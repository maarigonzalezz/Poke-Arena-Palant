BEGIN;

-- Solo desarrollo: inserta 12 cartas tras init.sql sin reemplazar códigos existentes.
INSERT INTO cards (
    code, name, cost, power,
    card_type, rarity, ability_code, image_status
)
VALUES
-- Comunes
--- Fuego
('PKA-FUE-901', 'Charmander', 2, 3,
 'Fuego', 'Común', 'HAB-000', 'Pending'),

('PKA-FUE-902', 'Ponyta', 1, 2,
 'Fuego', 'Común', 'HAB-000', 'Pending'),

('PKA-FUE-903', 'Flareon', 2, 3,
 'Fuego', 'Común', 'HAB-000', 'Pending'),

('PKA-FUE-904', 'Vulpix', 1, 2,
 'Fuego', 'Común', 'HAB-000', 'Pending'),

--- Agua
('PKA-AGU-901', 'Squirtle', 2, 3,
 'Agua', 'Común', 'HAB-000', 'Pending'),

('PKA-AGU-902', 'Psyduck', 2, 3,
 'Agua', 'Común', 'HAB-000', 'Pending'),

('PKA-AGU-903', 'Poliwag', 1, 2,
 'Agua', 'Común', 'HAB-000', 'Pending'),

('PKA-AGU-904', 'Magikarp', 1, 2,
 'Agua', 'Común', 'HAB-000', 'Pending'),

--- Electrico
('PKA-ELE-901', 'Pikachu', 2, 5,
 'Eléctrico', 'Común', 'HAB-000', 'Pending'),

('PKA-ELE-902', 'Jolteon', 2, 4,
 'Eléctrico', 'Común', 'HAB-000', 'Pending'),

('PKA-ELE-903', 'Pichu', 1, 3,
 'Eléctrico', 'Común', 'HAB-000', 'Pending'),

('PKA-ELE-904', 'Voltorb', 1, 3,
 'Eléctrico', 'Común', 'HAB-000', 'Pending'),

--- Planta
('PKA-PLA-901', 'Bulbasaur', 2, 3,
 'Planta', 'Común', 'HAB-000', 'Pending'),

('PKA-PLA-902', 'Caterpie', 1, 2,
 'Planta', 'Común', 'HAB-000', 'Pending'),

('PKA-PLA-903', 'Chikorita', 1, 2,
 'Planta', 'Común', 'HAB-000', 'Pending'),

('PKA-PLA-904', 'Bellsprout', 2, 3,
 'Planta', 'Común', 'HAB-000', 'Pending'),

--- Lucha
('PKA-LUC-901', 'Machop', 2, 4,
 'Lucha', 'Común', 'HAB-000', 'Pending'),

('PKA-LUC-902', 'Mankey', 1, 3,
 'Lucha', 'Común', 'HAB-000', 'Pending'),

('PKA-LUC-903', 'Cubone', 1, 3,
 'Lucha', 'Común', 'HAB-000', 'Pending'),

('PKA-LUC-904', 'Hitmonlee', 2, 4,
 'Lucha', 'Común', 'HAB-000', 'Pending'),

-- Poco comunes
('PKA-FUE-905', 'Charmeleon', 3, 6,
 'Fuego', 'Poco común', 'HAB-000', 'Pending'),

('PKA-AGU-905', 'Wartortle', 3, 6,
 'Agua', 'Poco común', 'HAB-000', 'Pending'),

('PKA-ELE-905', 'Raichu', 4, 8,
 'Eléctrico', 'Poco común', 'HAB-000', 'Pending'),

('PKA-PLA-905', 'Ivysaur', 3, 6,
 'Planta', 'Poco común', 'HAB-000', 'Pending'),

('PKA-LUC-905', 'Machoke', 4, 8,
 'Lucha', 'Poco común', 'HAB-000', 'Pending'),

-- Raras
('PKA-FUE-906', 'Charizard', 6, 12,
 'Fuego', 'Rara', 'HAB-000', 'Pending'),

('PKA-AGU-906', 'Blastoise', 6, 11,
 'Agua', 'Rara', 'HAB-000', 'Pending'),

('PKA-ELE-906', 'Zapdos', 7, 16,
 'Eléctrico', 'Rara', 'HAB-000', 'Pending'),

('PKA-PLA-906', 'Venusaur', 5, 12,
 'Planta', 'Rara', 'HAB-000', 'Pending'),

('PKA-LUC-906', 'Machamp', 6, 15,
 'Lucha', 'Rara', 'HAB-000', 'Pending'),

-- Legendarias
('PKA-PSI-901', 'Mewtwo', 6, 14,
 'Psíquico', 'Legendaria', 'HAB-000', 'Pending')

ON CONFLICT (code) DO NOTHING;

END;