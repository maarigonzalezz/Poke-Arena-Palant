-- SOLO PARA DESARROLLO Y PRUEBAS
-- No ejecutar en producción.
-- Inserta 12 cartas tras init.sql; los códigos existentes se conservan.

INSERT INTO cards (
    code, name, cost, power,
    card_type, rarity, ability_code, image_status
)
VALUES
('PKA-FUE-901', 'Charmander', 1, 3,
 'Fuego', 'Común', 'HAB-000', 'Pending'),

('PKA-FUE-902', 'Charmeleon', 3, 6,
 'Fuego', 'Poco común', 'HAB-000', 'Pending'),

('PKA-FUE-903', 'Charizard', 6, 12,
 'Fuego', 'Legendaria', 'HAB-000', 'Pending'),

('PKA-AGU-901', 'Squirtle', 1, 3,
 'Agua', 'Común', 'HAB-000', 'Pending'),

('PKA-AGU-902', 'Wartortle', 3, 6,
 'Agua', 'Poco común', 'HAB-000', 'Pending'),

('PKA-AGU-903', 'Blastoise', 6, 11,
 'Agua', 'Rara', 'HAB-000', 'Pending'),

('PKA-ELE-901', 'Pikachu', 2, 5,
 'Eléctrico', 'Común', 'HAB-000', 'Pending'),

('PKA-ELE-902', 'Raichu', 4, 8,
 'Eléctrico', 'Rara', 'HAB-000', 'Pending'),

('PKA-PLA-901', 'Bulbasaur', 1, 3,
 'Planta', 'Común', 'HAB-000', 'Pending'),

('PKA-PLA-902', 'Ivysaur', 3, 6,
 'Planta', 'Poco común', 'HAB-000', 'Pending'),

('PKA-PSI-901', 'Mewtwo', 6, 14,
 'Psíquico', 'Legendaria', 'HAB-000', 'Pending'),

('PKA-LUC-901', 'Machop', 1, 4,
 'Lucha', 'Común', 'HAB-000', 'Pending')
ON CONFLICT (code) DO NOTHING;
