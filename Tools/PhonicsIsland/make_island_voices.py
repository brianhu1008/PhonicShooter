# Records Phonics Island voice lines with a Microsoft Edge "Natural" voice (edge-tts, online).
# Input: one line per clip, "key<TAB>text" (UTF-8).  Output: <out_dir>/<key>.mp3
# Usage: python make_island_voices.py <list_file> <out_dir> <voice> <rate>
import asyncio
import os
import sys

import edge_tts


async def record(key, text, out_dir, voice, rate):
    path = os.path.join(out_dir, key + ".mp3")
    for attempt in range(4):
        try:
            await edge_tts.Communicate(text, voice, rate=rate).save(path + ".tmp")
            os.replace(path + ".tmp", path)
            return True
        except Exception as e:  # network hiccups: wait and retry
            print(f"retry {key}: {e}", file=sys.stderr)
            await asyncio.sleep(2 * (attempt + 1))
    return False


async def main():
    list_file, out_dir, voice, rate = sys.argv[1:5]
    os.makedirs(out_dir, exist_ok=True)
    rows = []
    with open(list_file, encoding="utf-8") as f:
        for row in f:
            key, _, text = row.rstrip("\n").partition("\t")
            if key and text:
                rows.append((key, text))
    sem = asyncio.Semaphore(4)

    async def one(key, text):
        async with sem:
            return await record(key, text, out_dir, voice, rate)

    results = await asyncio.gather(*(one(k, t) for k, t in rows))
    failed = [k for (k, _), ok in zip(rows, results) if not ok]
    print(f"Recorded {len(rows) - len(failed)} voice lines")
    if failed:
        print("FAILED: " + " ".join(failed), file=sys.stderr)
        sys.exit(1)


asyncio.run(main())
