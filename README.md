# Data Source

All data is downloaded from http://www.tanos.co.uk/jlpt/

## Build Command

```
yarn
yarn build
```

## Build Flow

- Anki files based on JLPT Level
- ↓ `anki2tabs.sh`
- Tabs files based on JLPT Level
- ↓ `tabs2json.js`
- JSON files based on JLPT Level
- ↓ `json2db.js`
- All in one JSON file

## Data Structure

```json
{
  "word": "毎朝",
  "meaning": {
    "en": "every morning",
    "es": ""
  },
  "hiragana": "まいあさ",
  "romaji": "maiasa",
  "level": 5,
  "uuid": "1adcbfaaea2489d61ef5122af3542a87"
}
```

`meaning.en` preserves the English meaning from the source dataset. `meaning.es`
is reserved for the Spanish translation and is currently empty; translations
will be added in a later pass. The JSON files in `db/` use this shape, and the
build process writes it for newly generated level files as well.

## Attribution and modifications

The vocabulary data originates from [tanos.co.uk/jlpt](https://www.tanos.co.uk/jlpt/)
and is attributed to Jonathan Waller under Creative Commons Attribution (CC BY).
Our local modification keeps the English meaning under `meaning.en` and adds an empty `meaning.es` field for future translation; Japanese words, readings, romaji, and JLPT levels are retained.
