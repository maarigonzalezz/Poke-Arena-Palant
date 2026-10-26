using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using SkiaSharp;

namespace PokeArena.Tests.Integration;

[Collection("catalog-integration")]
[Trait("Category","Integration")]
public sealed class CatalogApiTests(CatalogFixture fixture)
{
    private static readonly byte[] CardImage=CatalogImageTests.ImageBytes();
    private static readonly byte[] LocationImage=CatalogImageTests.ImageBytes(800,600);

    private static Dictionary<string,string> Card(string name="Volcán Ágil") => new()
    { ["name"]=name,["cost"]="2",["power"]="4",["type"]="Fuego",["rarity"]="Común",["abilityCode"]="HAB-001" };
    private static Dictionary<string,string> Location(string effect="None",string parameters="{}") => new()
    { ["name"]="Bosque",["description"]="Ubicación integrada",["maxCards"]="0",["energyMode"]="PerCard",["extraEnergy"]="2",["effectType"]=effect,["effectParameters"]=parameters };

    private static async Task<HttpResponseMessage> Post(HttpClient client,string route,Dictionary<string,string> values,
        byte[]? image,string? key=null,int fileCount=1)
    {
        using var form=new MultipartFormDataContent();
        foreach(var pair in values) form.Add(new StringContent(pair.Value),pair.Key);
        if(image is not null) for(var i=0;i<fileCount;i++) form.Add(new ByteArrayContent(image),"image","uploaded.png");
        using var request=new HttpRequestMessage(HttpMethod.Post,route) { Content=form };
        request.Headers.Add("Idempotency-Key",key??Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }

    private static async Task<JsonNode> Json(HttpResponseMessage response, HttpStatusCode expected=HttpStatusCode.OK)
    {
        var text=await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode==expected,$"Esperado {expected}, recibido {response.StatusCode}: {text}");
        return JsonNode.Parse(text)!;
    }

    [IntegrationFact] public async Task Credentials_and_admin_role_are_required_for_all_endpoints()
    {
        using var anonymous=fixture.Client(null); using var player=fixture.Client("Player");
        foreach(var route in new[]{"/api/cards","/api/abilities"})
        {
            await Json(await anonymous.GetAsync(route),HttpStatusCode.Unauthorized);
            await Json(await player.GetAsync(route),HttpStatusCode.Forbidden);
        }
        foreach(var route in new[]{"/api/cards","/api/locations"})
        {
            await Json(await Post(anonymous,route,Card(),CardImage),HttpStatusCode.Unauthorized);
            await Json(await Post(player,route,Card(),CardImage),HttpStatusCode.Forbidden);
        }
    }

    [IntegrationFact] public async Task Create_card_publishes_both_images_and_is_searchable_with_combined_filters()
    {
        await fixture.Reset(); using var client=fixture.Client();
        var abilities=(await Json(await client.GetAsync("/api/abilities"))).AsArray();
        Assert.Contains(abilities,a=>a!["code"]!.GetValue<string>()=="HAB-000");
        var created=(await Json(await Post(client,"/api/cards",Card(),CardImage),HttpStatusCode.Created))["item"]!;
        Assert.Equal("PKA-FUE-001",created["code"]!.GetValue<string>());
        Assert.Equal("Ready",created["imageStatus"]!.GetValue<string>());
        Assert.Equal(CardImage,await client.GetByteArrayAsync(created["imageUrl"]!.GetValue<string>()));
        using var small=SKBitmap.Decode(await client.GetByteArrayAsync(created["imageSmallUrl"]!.GetValue<string>()));
        Assert.Equal(200,small.Width); Assert.Equal(280,small.Height);
        var found=await Json(await client.GetAsync("/api/cards?name=VOLCAN&type=Fuego&rarity=Com%C3%BAn&minCost=1&maxCost=3&power=4&abilityCode=HAB-001&abilityTrigger=AlRevelarse&abilityEffect=AumentarPoder"));
        Assert.Single(found["items"]!.AsArray());
        Assert.Contains("power",found["items"]![0]!["abilityParameters"]!.GetValue<string>());
        Assert.Equal(1,await fixture.Count("SELECT COUNT(*) FROM playable_cards"));
        Assert.Equal(1,await fixture.Count("SELECT COUNT(*) FROM card_versions"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>fixture.Execute("UPDATE cards SET code='PKA-FUE-099'"));
        var none=Card("Sin habilidad"); none["abilityCode"]="HAB-000";
        await Json(await Post(client,"/api/cards",none,CardImage),HttpStatusCode.Created);
        var noAbility=await Json(await client.GetAsync("/api/cards?abilityCode=HAB-000"));
        Assert.Single(noAbility["items"]!.AsArray());
        var empty=await Json(await client.GetAsync("/api/cards?name=NOEXISTE"));
        Assert.Empty(empty["items"]!.AsArray());
    }

    [IntegrationFact] public async Task Search_migration_repairs_existing_database_and_preserves_cards_and_sessions()
    {
        await fixture.Reset(); using var client=fixture.Client();
        var created=(await Json(await Post(client,"/api/cards",Card(),CardImage),HttpStatusCode.Created))["item"]!;
        // Solo el contenedor desechable: reproduce una base anterior a las tablas de búsqueda.
        await fixture.Execute("DROP TABLE card_search_items; DROP TABLE card_search_sessions;");
        var migration=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"TestData/search_sessions.sql"));
        try
        {
            await fixture.Execute(migration);
            var found=await Json(await client.GetAsync("/api/cards?name=VOLCAN&type=Fuego&cost=2&power=4"));
            Assert.Single(found["items"]!.AsArray());
            Assert.Equal(created["code"]!.GetValue<string>(),found["items"]![0]!["code"]!.GetValue<string>());
            await fixture.Execute(migration);
            var page=await Json(await client.GetAsync("/api/cards?searchId="+found["searchId"]!.GetValue<string>()));
            Assert.Single(page["items"]!.AsArray());
            Assert.Equal(1,await fixture.Count("SELECT COUNT(*) FROM cards"));
            Assert.Equal(1,await fixture.Count("SELECT COUNT(*) FROM card_versions"));
        }
        finally { await fixture.Execute(migration); }
    }

    [IntegrationFact] public async Task Creation_is_idempotent_concurrent_and_scoped_to_user_and_operation()
    {
        await fixture.Reset(); using var client=fixture.Client();
        var key=Guid.NewGuid().ToString();
        var responses=await Task.WhenAll(Enumerable.Range(0,5).Select(_=>Post(client,"/api/cards",Card(),CardImage,key)));
        var codes=new List<string>();
        foreach(var response in responses) codes.Add((await Json(response,HttpStatusCode.Created))["item"]!["code"]!.GetValue<string>());
        Assert.Single(codes.Distinct()); Assert.Equal(1,await fixture.Count("SELECT COUNT(*) FROM cards"));
        await Json(await Post(client,"/api/cards",Card("Otro contenido"),CardImage,key),HttpStatusCode.Conflict);
        await Json(await Post(client,"/api/cards",Card(),CardImage.Concat(new byte[]{0}).ToArray(),key),HttpStatusCode.Conflict);
        var distinct=await Task.WhenAll(Enumerable.Range(0,8).Select(i=>Post(client,"/api/cards",Card("Carta "+i),CardImage)));
        foreach(var response in distinct) await Json(response,HttpStatusCode.Created);
        Assert.Equal(9,await fixture.Count("SELECT COUNT(DISTINCT code) FROM cards"));
        Assert.Equal(9,await fixture.Count("SELECT last_number FROM card_code_sequences WHERE card_type='Fuego'"));
        using var other=fixture.Client(actor:"another-admin");
        await Json(await Post(other,"/api/cards",Card(),CardImage,key),HttpStatusCode.Created);
        await Json(await Post(client,"/api/locations",Location(),LocationImage,key),HttpStatusCode.Created);
    }

    [IntegrationFact] public async Task Rejects_invalid_or_missing_fields_and_bad_images_without_partial_records()
    {
        await fixture.Reset(); using var client=fixture.Client();
        var variants=new[] { ("name"," "),("name",new string('x',121)),("cost","11"),("cost","2.5"),("power","-21"),
            ("type","Otro"),("rarity","Otra"),("abilityCode","NOEXISTE"),("type","Agua") };
        foreach(var (field,value) in variants)
        {
            var fields=Card(); fields[field]=value;
            await Json(await Post(client,"/api/cards",fields,CardImage),HttpStatusCode.BadRequest);
        }
        var missing=Card(); missing.Remove("cost");
        await Json(await Post(client,"/api/cards",missing,CardImage),HttpStatusCode.BadRequest);
        foreach(var bytes in new byte[]?[]{null,Array.Empty<byte>(),new byte[]{1,2,3},new byte[5*1024*1024+1],CatalogImageTests.ImageBytes(800,600)})
            await Json(await Post(client,"/api/cards",Card(),bytes),HttpStatusCode.BadRequest);
        await Json(await Post(client,"/api/cards",Card(),CardImage,fileCount:2),HttpStatusCode.BadRequest);
        await Json(await Post(client,"/api/cards",Card(),CardImage,key:""),HttpStatusCode.BadRequest);
        Assert.Equal(0,await fixture.Count("SELECT COUNT(*) FROM cards"));
        Assert.Equal(0,await fixture.Count("SELECT COUNT(*) FROM idempotency_requests"));
        Assert.Equal(0,await fixture.Count("SELECT last_number FROM card_code_sequences WHERE card_type='Fuego'"));
    }

    [IntegrationFact] public async Task Locations_save_images_versions_all_effects_and_validate_limits()
    {
        await fixture.Reset(); using var client=fixture.Client();
        foreach(var (effect,parameters) in new[]{("None","{}"),("TypeBonus","{\"type\":\"Fuego\",\"power\":2}"),
            ("CostModifier","{\"amount\":-1,\"affectedType\":null}"),("TurnRestriction","{\"turns\":[1,6]}")})
        {
            var key=Guid.NewGuid().ToString(); var values=Location(effect,parameters);
            var created=await Json(await Post(client,"/api/locations",values,LocationImage,key),HttpStatusCode.Created);
            var repeated=await Json(await Post(client,"/api/locations",values,LocationImage,key),HttpStatusCode.Created);
            Assert.Equal(created["id"]!.GetValue<long>(),repeated["id"]!.GetValue<long>());
            Assert.Equal("Ready",created["imageStatus"]!.GetValue<string>());
            Assert.Equal(LocationImage,await client.GetByteArrayAsync(created["imageOriginalPath"]!.GetValue<string>()));
            using var small=SKBitmap.Decode(await client.GetByteArrayAsync(created["imageSmallPath"]!.GetValue<string>()));
            Assert.Equal(320,small.Width); Assert.Equal(240,small.Height);
        }
        Assert.Equal(4,await fixture.Count("SELECT COUNT(*) FROM available_locations"));
        Assert.Equal(4,await fixture.Count("SELECT COUNT(*) FROM location_versions"));
        foreach(var (field,value) in new[]{("maxCards","6"),("extraEnergy","3"),("description",""),("effectParameters","{invalid}"),("effectType","Otro")})
        {
            var values=Location(); values[field]=value;
            await Json(await Post(client,"/api/locations",values,LocationImage),HttpStatusCode.BadRequest);
        }
        await Json(await Post(client,"/api/locations",Location("TurnRestriction","{\"turns\":[1,1]}"),LocationImage),HttpStatusCode.BadRequest);
        await Json(await Post(client,"/api/locations",Location(),null),HttpStatusCode.BadRequest);
        Assert.Equal(4,await fixture.Count("SELECT COUNT(*) FROM locations"));
    }

    [IntegrationFact] public async Task Search_has_bounded_pages_and_stable_order_across_thousands_and_concurrent_creation()
    {
        await fixture.Reset(); using var client=fixture.Client();
        await fixture.Execute("""
            INSERT INTO cards(code,name,cost,power,card_type,rarity,ability_code)
            SELECT 'PKA-' || types.prefix || '-' || LPAD(n::text,3,'0'), 'Carta ' || types.prefix || n,
                n%11, (n%41)-20, types.name, 'Común','HAB-000'
            FROM generate_series(1,500) n CROSS JOIN
                (VALUES ('FUE','Fuego'),('AGU','Agua'),('PLA','Planta'),('ELE','Eléctrico'),('PSI','Psíquico'),('LUC','Lucha')) types(prefix,name)
            """);
        var timer=Stopwatch.StartNew();
        var first=await Json(await client.GetAsync("/api/cards?pageSize=100"));
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(10));
        Assert.Equal(3000,first["totalCount"]!.GetValue<int>());
        await Json(await Post(client,"/api/cards",Card("Carta nueva"),CardImage),HttpStatusCode.Created);
        var seen=new HashSet<string>(); var page=first;
        while(true)
        {
            Assert.InRange(page["items"]!.AsArray().Count,1,100);
            Assert.True(page.ToJsonString().Length < 200_000);
            foreach(var item in page["items"]!.AsArray()) Assert.True(seen.Add(item!["code"]!.GetValue<string>()));
            if(page["nextPosition"] is null) break;
            page=await Json(await client.GetAsync($"/api/cards?searchId={first["searchId"]}&after={page["nextPosition"]}&pageSize=100"));
        }
        Assert.Equal(3000,seen.Count);
        Assert.DoesNotContain("PKA-FUE-501",seen);
        var fresh=await Json(await client.GetAsync("/api/cards"));
        Assert.Equal(3001,fresh["totalCount"]!.GetValue<int>());
        using var other=fixture.Client(actor:"other-admin");
        await Json(await other.GetAsync($"/api/cards?searchId={first["searchId"]}&after=100"),HttpStatusCode.NotFound);
        await fixture.Execute("UPDATE card_search_sessions SET expires_at=NOW()-INTERVAL '1 minute'");
        await Json(await client.GetAsync($"/api/cards?searchId={first["searchId"]}&after=100"),HttpStatusCode.NotFound);
    }

    [IntegrationFact] public async Task Database_failure_removes_images_rolls_back_code_and_allows_retry()
    {
        await fixture.Reset(); using var client=fixture.Client();
        var filesBefore=fixture.StoredFiles;
        await fixture.Execute("""
            CREATE OR REPLACE FUNCTION fail_test_insert() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'fallo de prueba'; END; $$;
            CREATE TRIGGER test_failure BEFORE INSERT ON cards FOR EACH ROW EXECUTE FUNCTION fail_test_insert();
            """);
        var key=Guid.NewGuid().ToString();
        try
        {
            await Json(await Post(client,"/api/cards",Card(),CardImage,key),HttpStatusCode.ServiceUnavailable);
            Assert.Equal(filesBefore,fixture.StoredFiles);
            Assert.Equal(0,await fixture.Count("SELECT COUNT(*) FROM idempotency_requests"));
            Assert.Equal(0,await fixture.Count("SELECT last_number FROM card_code_sequences WHERE card_type='Fuego'"));
        }
        finally { await fixture.Execute("DROP TRIGGER test_failure ON cards; DROP FUNCTION fail_test_insert()"); }
        var created=await Json(await Post(client,"/api/cards",Card(),CardImage,key),HttpStatusCode.Created);
        Assert.Equal("PKA-FUE-001",created["item"]!["code"]!.GetValue<string>());
    }

    [IntegrationFact] public async Task Invalid_search_and_exhausted_codes_return_explicit_errors()
    {
        await fixture.Reset(); using var client=fixture.Client();
        foreach(var query in new[]{"cost=11","power=-21","type=Otro","rarity=Otra","minCost=5&maxCost=2","cost=0&minCost=0","pageSize=101","after=1"})
        {
            var error=await Json(await client.GetAsync("/api/cards?"+query),HttpStatusCode.BadRequest);
            Assert.NotNull(error["errors"]);
        }
        await fixture.Execute("UPDATE card_code_sequences SET last_number=999 WHERE card_type='Fuego'");
        await Json(await Post(client,"/api/cards",Card(),CardImage),HttpStatusCode.Conflict);
        Assert.Equal(0,await fixture.Count("SELECT COUNT(*) FROM cards"));
    }
}
