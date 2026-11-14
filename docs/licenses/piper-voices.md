# Piper voice-model notices

Luna-Quest ships **generated audio**, not the Piper models themselves. The MP3 files in
`assets/audio/voice/` were produced at development time with
[Piper](https://github.com/rhasspy/piper) (MIT license) from the voice models below.

Every line is synthetic speech for a fictional colony character. The crew names in
`content/npcs.json` are not the identities of the dataset speakers.

## Models used by the current pack

| Model | Character | Upstream model card | Dataset license |
|---|---|---|---|
| [`en_GB-cori-high`](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_GB/cori/high) | Dr. Safaa | `en/en_GB/cori/high/MODEL_CARD` | Public domain (LibriVox, <https://librivox.org>) |
| [`en_US-john-medium`](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/john/medium) | Noor | `en/en_US/john/medium/MODEL_CARD` | Public domain (LibriVox, <https://librivox.org>) |
| [`en_US-norman-medium`](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/norman/medium) | Dr. Alaa | `en/en_US/norman/medium/MODEL_CARD` | Public domain (LibriVox, <https://librivox.org>) |
| [`en_US-kathleen-medium`](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/kathleen/medium) | Agriculture and Food Engineer | `en/en_US/kathleen/medium/MODEL_CARD` | CC0 (<https://github.com/OHF-Voice/voice-datasets>) |
| [`en_US-joe-medium`](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/joe/medium) | Abdel Aziz | `en/en_US/joe/medium/MODEL_CARD` | CC0 (<https://github.com/OHF-Voice/voice-datasets>) |
| [`en_US-bryce-medium`](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/bryce/medium) | Ahmed | `en/en_US/bryce/medium/MODEL_CARD` | Public domain, per the upstream model card |
| [`en_GB-alba-medium`](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_GB/alba/medium) | Communications and Software Engineer | `en/en_GB/alba/medium/MODEL_CARD` | CC BY 4.0 (<https://datashare.ed.ac.uk/handle/10283/3270>) |
| [`en_US-mike-medium`](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/mike/medium) | Mostafa | `en/en_US/mike/medium/MODEL_CARD` | CC0 (<https://github.com/OHF-Voice/voice-datasets>) |

## Attribution

If you redistribute the MP3 files, keep the table above and this notice. The
`en_GB-alba-medium` model is CC BY 4.0; credit the upstream dataset and the Piper project.
The other seven models used here are public domain or CC0.

The Arabic text in `content/voices.json` and `pack.json` is a subtitle translation, not
Arabic speech. No Arabic TTS model is used, and the game does not claim to teach Arabic
pronunciation.

## Regenerating

The generator is a development tool and is not part of the runtime:

```bash
PIPER_BIN=/path/to/piper \
PIPER_MODELS=/path/to/piper/voices \
python3 tools/generate_voice_pack.py --force
```

The source of truth for the cast and every line is `content/voices.json`. Changing a model
or a line means regenerating the pack, copying it with `tools/sync-content.sh`, and letting
Godot reimport the MP3 files.
