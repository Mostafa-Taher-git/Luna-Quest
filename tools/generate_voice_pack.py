#!/usr/bin/env python3
"""
Generate the Luna-Quest voice pack from content/voices.json.

The game never calls a cloud service at runtime: this is a development-time tool that
writes real MP3 files plus the pack.json the Godot and Unity adapters read.

Requirements
------------
* Piper (https://github.com/rhasspy/piper) with its voice models downloaded locally.
* ffmpeg on PATH.

Example
-------
    PIPER_BIN=/tmp/opencode/bin/piper \
    PIPER_MODELS=/tmp/opencode/piper/voices \
    python3 tools/generate_voice_pack.py

The generated audio is English. Arabic subtitles travel in pack.json so the game
stays readable in both languages; no Arabic TTS model is required.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path


def slug_for_key(key: str) -> str:
    if key.startswith("inc."):
        incident = key[4:]
        if incident.startswith("inc_"):
            incident = incident[4:]
        return "inc-" + incident.replace("_", "-") + ".mp3"
    return key.replace(".", "-") + ".mp3"


def run(cmd: list[str], **kwargs) -> subprocess.CompletedProcess:
    result = subprocess.run(cmd, **kwargs)
    if result.returncode != 0:
        raise RuntimeError(f"command failed ({result.returncode}): {' '.join(cmd)}")
    return result


def main() -> int:
    root = Path(__file__).resolve().parent.parent
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", default=str(root / "content" / "voices.json"))
    parser.add_argument("--out", default=str(root / "assets" / "audio" / "voice"))
    parser.add_argument("--models", default=os.environ.get("PIPER_MODELS", ""),
                        help="directory containing <model>.onnx and <model>.onnx.json")
    parser.add_argument("--piper", default=os.environ.get("PIPER_BIN", "piper"))
    parser.add_argument("--ffmpeg", default=shutil.which("ffmpeg") or "ffmpeg")
    parser.add_argument("--force", action="store_true", help="regenerate files that already exist")
    args = parser.parse_args()

    manifest_path = Path(args.manifest)
    out_dir = Path(args.out)
    models_dir = Path(args.models) if args.models else None
    if models_dir is None:
        print("error: pass --models or set PIPER_MODELS", file=sys.stderr)
        return 2
    if not Path(args.piper).exists() and shutil.which(args.piper) is None:
        print(f"error: piper executable not found: {args.piper}", file=sys.stderr)
        return 2

    data = json.loads(manifest_path.read_text(encoding="utf-8"))
    cast = data["cast"]
    lines = list(data["lines"])

    # Crew topics live in content/dialogue.json so the label, the subtitle and the
    # spoken line share one source. They are merged into the same generated pack.
    dialogue_path = root / "content" / "dialogue.json"
    topic_count = 0
    if dialogue_path.exists():
        dialogue = json.loads(dialogue_path.read_text(encoding="utf-8"))
        for topic in dialogue.get("topics", []):
            lines.append({
                "key": topic["voiceKey"],
                "speaker": topic["npcId"],
                "text": topic["lineEn"],
                "textAr": topic["lineAr"],
            })
            topic_count += 1

    seen = set()
    for line in lines:
        if line["key"] in seen:
            print(f"error: duplicate voice key {line['key']}", file=sys.stderr)
            return 2
        seen.add(line["key"])
    out_dir.mkdir(parents=True, exist_ok=True)

    missing_models = sorted({
        entry["model"] for entry in cast.values()
        if not (models_dir / f"{entry['model']}.onnx").exists()
    })
    if missing_models:
        print("error: missing Piper models in " + str(models_dir) + ":", file=sys.stderr)
        for model in missing_models:
            print(f"  {model}.onnx", file=sys.stderr)
        return 2

    pack: dict[str, dict[str, str]] = {}
    generated = 0
    skipped = 0

    for index, line in enumerate(lines, start=1):
        key = line["key"]
        speaker = line["speaker"]
        if speaker not in cast:
            print(f"error: line {key} names unknown speaker {speaker}", file=sys.stderr)
            return 2
        profile = cast[speaker]
        model = models_dir / f"{profile['model']}.onnx"
        file_name = slug_for_key(key)
        target = out_dir / file_name

        if target.exists() and not args.force:
            skipped += 1
        else:
            with tempfile.TemporaryDirectory(prefix="luna-voice-") as tmp:
                wav = Path(tmp) / "line.wav"
                run(
                    [
                        args.piper,
                        "-m", str(model),
                        "-f", str(wav),
                        "--length-scale", str(profile.get("lengthScale", 1.0)),
                        "--noise-scale", str(profile.get("noiseScale", 0.667)),
                        "--sentence-silence", "0.18",
                    ],
                    input=line["text"].encode("utf-8"),
                    check=False,
                )
                # Normalise to a consistent mono 22.05 kHz, 64 kbps pack. Speech only,
                # so a modest bitrate keeps the repository small without artefacts.
                run(
                    [
                        args.ffmpeg, "-hide_banner", "-loglevel", "error", "-y",
                        "-i", str(wav),
                        "-ac", "1", "-ar", "22050", "-codec:a", "libmp3lame", "-b:a", "64k",
                        "-af", "loudnorm=I=-18:TP=-2:LRA=7",
                        str(target),
                    ],
                    check=False,
                )
            generated += 1

        pack[key] = {
            "file": file_name,
            "text": line["text"],
            "textAr": line.get("textAr", line["text"]),
            "speaker": speaker,
            "model": profile["model"],
        }
        print(f"[{index:02d}/{len(lines)}] {key:28} -> {file_name}")

    pack_path = out_dir / "pack.json"
    pack_path.write_text(
        json.dumps(pack, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    # A small provenance file is easier to audit than a chat log.
    provenance = {
        "provider": data.get("provider", "piper"),
        "comment": data.get("comment", ""),
        "sourceManifest": str(manifest_path.relative_to(root)),
        "sourceDialogue": str(dialogue_path.relative_to(root)) if dialogue_path.exists() else None,
        "cast": {
            npc: {
                "prefix": entry["prefix"],
                "model": entry["model"],
                "lengthScale": entry.get("lengthScale", 1.0),
                "noiseScale": entry.get("noiseScale", 0.667),
            }
            for npc, entry in cast.items()
        },
        "lineCount": len(lines),
        "topicLines": topic_count,
        "generatedThisRun": generated,
        "reusedThisRun": skipped,
        "runtimeCloudTts": False,
        "language": "en",
        "subtitles": ["en", "ar"],
    }
    (out_dir / "manifest.json").write_text(
        json.dumps(provenance, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )

    print(f"pack.json: {len(pack)} lines; generated {generated}, reused {skipped}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
