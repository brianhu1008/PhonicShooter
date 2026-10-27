"""Make a small bold subset of Noto Sans TC with only the characters the game uses
(the web version can't use the computer's fonts). Run again after adding Chinese text.
Font source: https://github.com/google/fonts/tree/main/ofl/notosanstc  (SIL OFL 1.1)
usage: py -I Tools/make_font.py <NotoSansTC[wght].ttf> Assets/Resources/Fonts/GameFont.ttf Assets
OFL: a modified font may not keep the reserved name, so the family is renamed."""
import sys, pathlib
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer
from fontTools import subset

src, out, assets = sys.argv[1], sys.argv[2], pathlib.Path(sys.argv[3])

chars = set(chr(c) for c in range(0x20, 0x7F))           # all printable ASCII
chars |= set("，。！？、：；「」（）『』…－～·／")
for f in assets.glob("*.cs"):
    for ch in f.read_text(encoding="utf-8"):
        if ord(ch) > 0x7F:
            chars.add(ch)

font = TTFont(src)
font = instancer.instantiateVariableFont(font, {"wght": 700})
opts = subset.Options()
opts.layout_features = ["*"]
opts.name_IDs = ["*"]
sub = subset.Subsetter(opts)
sub.populate(text="".join(chars))
sub.subset(font)

for rec in font["name"].names:
    if rec.nameID in (1, 4, 16):
        rec.string = "Phonics Island Font"
    elif rec.nameID == 6:
        rec.string = "PhonicsIslandFont-Bold"
    elif rec.nameID == 3:
        rec.string = "PhonicsIslandFont-Bold-subset"
font.save(out)
cjk = sum(1 for c in chars if ord(c) >= 0x2E80)
print(f"{len(chars)} characters ({cjk} CJK) -> {out} ({pathlib.Path(out).stat().st_size // 1024} KB)")
