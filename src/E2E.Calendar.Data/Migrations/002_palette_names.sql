-- Migrate stored calendar/event colours from legacy hex values to palette names
-- (docs/11-THEME.md §4). Unknown values fall back to Slate.

UPDATE calendar
SET color = CASE lower(trim(color))
    WHEN '#0078d4' THEN 'Blue'
    WHEN '#8764b8' THEN 'Purple'
    WHEN '#c239b3' THEN 'Pink'
    WHEN '#00b7c3' THEN 'Teal'
    WHEN '#ff8c00' THEN 'Orange'
    WHEN '#00b050' THEN 'Green'
    WHEN '#e3008c' THEN 'Pink'
    WHEN '#986f0b' THEN 'Amber'
    WHEN '#004e8c' THEN 'Blue'
    WHEN '#e81123' THEN 'Red'
    WHEN '#6a00c4' THEN 'Purple'
    WHEN '#767676' THEN 'Slate'
END
WHERE lower(trim(color)) IN (
    '#0078d4', '#8764b8', '#c239b3', '#00b7c3', '#ff8c00', '#00b050',
    '#e3008c', '#986f0b', '#004e8c', '#e81123', '#6a00c4', '#767676'
);

UPDATE calendar
SET color = 'Slate'
WHERE color IS NOT NULL
  AND lower(color) NOT IN (
    'indigo', 'blue', 'teal', 'green', 'lime', 'amber',
    'orange', 'red', 'pink', 'purple', 'slate', 'brown'
  );

UPDATE event
SET color = CASE lower(trim(color))
    WHEN '#0078d4' THEN 'Blue'
    WHEN '#8764b8' THEN 'Purple'
    WHEN '#c239b3' THEN 'Pink'
    WHEN '#00b7c3' THEN 'Teal'
    WHEN '#ff8c00' THEN 'Orange'
    WHEN '#00b050' THEN 'Green'
    WHEN '#e3008c' THEN 'Pink'
    WHEN '#986f0b' THEN 'Amber'
    WHEN '#004e8c' THEN 'Blue'
    WHEN '#e81123' THEN 'Red'
    WHEN '#6a00c4' THEN 'Purple'
    WHEN '#767676' THEN 'Slate'
END
WHERE lower(trim(color)) IN (
    '#0078d4', '#8764b8', '#c239b3', '#00b7c3', '#ff8c00', '#00b050',
    '#e3008c', '#986f0b', '#004e8c', '#e81123', '#6a00c4', '#767676'
);

UPDATE event
SET color = 'Slate'
WHERE color IS NOT NULL
  AND lower(color) NOT IN (
    'indigo', 'blue', 'teal', 'green', 'lime', 'amber',
    'orange', 'red', 'pink', 'purple', 'slate', 'brown'
  );
