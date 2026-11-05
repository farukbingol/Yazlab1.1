#!/usr/bin/env python3
"""Quadlet piksel fontuna eksik Türkçe harfleri (ş Ş ğ Ğ İ) ve — • … işaretlerini ekler.

Quadlet yalnız Latin-1 içerir. Lisansı fontu değiştirmeye izin verir ama dosyanın açık repoya konmasını yasaklar;
bu yüzden hem orijinal hem yamalı font git dışındadır, repoda yalnız bu betik durur.

Kullanım (fonttools gerekir: pip install fonttools):
    python3 Tools/quadlet_tr.py Assets/quadlet-fonts/Quadlet/Quadlet.ttf \
        Assets/_Project/LocalOnly/Resources/PixelFonts/Quadlet-TR.ttf
"""
import os
import sys

from fontTools.pens.pointInsidePen import PointInsidePen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont

PX = 64  # Quadlet'te bir piksel = 64 font birimi (11 px = 704 birim)

# Yeni harf: (taban harf, eklenecek pikseller) — taban harfin bitmap'i alınıp üstüne çengel/şapka eklenir.
# Tabansız işaret: (ilerleme genişliği px, pikseller). Koordinat (x, y); y=0 taban çizgisi.
CEDILLA = {(2, -1), (1, -2)}  # ç'deki çengelin aynısı
GLYPHS = {
    'ş': ('s', CEDILLA),
    'Ş': ('S', CEDILLA),
    'ğ': ('g', {(0, 6), (3, 6), (1, 5), (2, 5)}),
    'Ğ': ('G', {(0, 8), (3, 8), (1, 7), (2, 7)}),
    'İ': ('I', {(1, 7)}),
    '—': (8, {(x, 2) for x in range(7)}),
    '•': (3, {(0, 2), (1, 2), (0, 3), (1, 3)}),
    '…': (6, {(0, 0), (2, 0), (4, 0)}),
}


def bitmap(font, char):
    """Harfin dolu piksellerini, piksel merkezlerinde içerde/dışarda testiyle çıkarır."""
    glyph_set = font.getGlyphSet()
    name = font.getBestCmap()[ord(char)]
    pixels = set()
    for y in range(-3, 10):
        for x in range(8):
            pen = PointInsidePen(glyph_set, (x * PX + PX / 2, y * PX + PX / 2))
            glyph_set[name].draw(pen)
            if pen.getResult():
                pixels.add((x, y))
    return pixels


def draw(pixels):
    """Pikselleri satır satır yatay dikdörtgenler olarak çizer (orijinal fonttaki gibi, saat yönünde)."""
    pen = TTGlyphPen(None)
    for y in sorted({p[1] for p in pixels}):
        xs = sorted(x for x, row in pixels if row == y)
        start = xs[0]
        for a, b in zip(xs, xs[1:] + [None]):
            if b != a + 1:
                pen.moveTo((start * PX, y * PX))
                pen.lineTo((start * PX, (y + 1) * PX))
                pen.lineTo(((a + 1) * PX, (y + 1) * PX))
                pen.lineTo(((a + 1) * PX, y * PX))
                pen.closePath()
                start = b
    return pen.glyph()


def main(src, dst):
    font = TTFont(src)
    cmap = font.getBestCmap()
    order = font.getGlyphOrder()
    unicode_tables = [t for t in font['cmap'].tables if t.isUnicode()]
    for char, (base, extra) in GLYPHS.items():
        if ord(char) in cmap:
            continue
        if isinstance(base, str):
            pixels = bitmap(font, base) | extra
            advance = font['hmtx'][cmap[ord(base)]][0]
        else:
            pixels, advance = extra, base * PX
        name = 'uni%04X' % ord(char)
        order.append(name)
        font['glyf'][name] = draw(pixels)
        font['hmtx'][name] = (advance, 0)
        for table in unicode_tables:
            table.cmap[ord(char)] = name

    font.setGlyphOrder(order)
    font['maxp'].numGlyphs = len(order)
    # Ayrı aile adı: orijinal Quadlet ile karışmasın.
    for record in font['name'].names:
        if record.nameID in (1, 3, 4):
            record.string = record.toUnicode().replace('Quadlet', 'Quadlet TR', 1)
        elif record.nameID == 6:
            record.string = record.toUnicode().replace('Quadlet', 'QuadletTR', 1)

    os.makedirs(os.path.dirname(os.path.abspath(dst)), exist_ok=True)
    font.save(dst)
    print(f'{dst}: {len(order)} glif')


if __name__ == '__main__':
    if len(sys.argv) != 3:
        sys.exit(__doc__)
    main(sys.argv[1], sys.argv[2])
