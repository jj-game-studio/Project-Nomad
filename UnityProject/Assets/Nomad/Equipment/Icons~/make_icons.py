# 장비 아이콘 생성기입니다. 외부 라이브러리 없이 32단위 도형을 128px PNG로 그립니다.
# 실행: python make_icons.py  (결과는 ../Icons/<item_id>.png)
import math
import os
import struct
import zlib

SIZE = 128
SS = 3  # 계단 현상을 줄이는 초과 표본 배율
UNIT = SIZE * SS / 32.0
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Icons")
INK = (28, 24, 22)


def hexc(value):
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def shade(color, amount):
    return tuple(max(0, min(255, int(c * amount))) for c in color)


class Canvas:
    def __init__(self):
        n = SIZE * SS
        self.n = n
        self.px = [[None] * n for _ in range(n)]

    def fill(self, inside, color, box):
        x0, y0, x1, y1 = (max(0, int(v * UNIT) - 1) if i < 2 else min(self.n, int(v * UNIT) + 2) for i, v in enumerate(box))
        for y in range(y0, y1):
            fy = (y + 0.5) / UNIT
            row = self.px[y]
            for x in range(x0, x1):
                if inside((x + 0.5) / UNIT, fy):
                    row[x] = color

    # 외곽선은 같은 도형을 8방향으로 밀어 어두운 색으로 먼저 칠합니다.
    def shape(self, make, color, outline=True):
        if outline:
            for dx, dy in [(-1, 0), (1, 0), (0, -1), (0, 1), (-0.7, -0.7), (0.7, -0.7), (-0.7, 0.7), (0.7, 0.7)]:
                inside, box = make(dx * 0.9, dy * 0.9)
                self.fill(inside, INK, box)
        inside, box = make(0, 0)
        self.fill(inside, color, box)

    def png(self, path):
        raw = bytearray()
        for y in range(SIZE):
            raw.append(0)
            for x in range(SIZE):
                r = g = b = a = 0
                for sy in range(SS):
                    for sx in range(SS):
                        c = self.px[y * SS + sy][x * SS + sx]
                        if c is not None:
                            r += c[0]; g += c[1]; b += c[2]; a += 1
                total = SS * SS
                if a:
                    raw += bytes((r // a, g // a, b // a, 255 * a // total))
                else:
                    raw += b"\0\0\0\0"

        def chunk(kind, data):
            body = kind + data
            return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

        header = struct.pack(">IIBBBBB", SIZE, SIZE, 8, 6, 0, 0, 0)
        with open(path, "wb") as f:
            f.write(b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", header) + chunk(b"IDAT", zlib.compress(bytes(raw), 9)) + chunk(b"IEND", b""))


def poly(points):
    def make(dx, dy):
        pts = [(x + dx, y + dy) for x, y in points]
        xs, ys = [p[0] for p in pts], [p[1] for p in pts]

        def inside(x, y):
            hit = False
            j = len(pts) - 1
            for i in range(len(pts)):
                xi, yi = pts[i]; xj, yj = pts[j]
                if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
                    hit = not hit
                j = i
            return hit
        return inside, (min(xs), min(ys), max(xs), max(ys))
    return make


def ellipse(cx, cy, rx, ry, inner=0.0, top_only=False):
    def make(dx, dy):
        x0, y0 = cx + dx, cy + dy

        def inside(x, y):
            if top_only and y > y0:
                return False
            d = ((x - x0) / rx) ** 2 + ((y - y0) / ry) ** 2
            return d <= 1 and (inner == 0 or d >= inner * inner)
        return inside, (x0 - rx, y0 - ry, x0 + rx, y0 + ry)
    return make


def rect(x0, y0, x1, y1, radius=0.0):
    def make(dx, dy):
        a, b, c, d = x0 + dx, y0 + dy, x1 + dx, y1 + dy

        def inside(x, y):
            if not (a <= x <= c and b <= y <= d):
                return False
            if radius <= 0:
                return True
            qx = max(a + radius - x, 0, x - (c - radius))
            qy = max(b + radius - y, 0, y - (d - radius))
            return qx * qx + qy * qy <= radius * radius
        return inside, (a, b, c, d)
    return make


def line(x0, y0, x1, y1, width):
    def make(dx, dy):
        ax, ay, bx, by = x0 + dx, y0 + dy, x1 + dx, y1 + dy
        length = max(1e-6, (bx - ax) ** 2 + (by - ay) ** 2)

        def inside(x, y):
            t = max(0, min(1, ((x - ax) * (bx - ax) + (y - ay) * (by - ay)) / length))
            px, py = ax + t * (bx - ax), ay + t * (by - ay)
            return (x - px) ** 2 + (y - py) ** 2 <= (width / 2) ** 2
        return inside, (min(ax, bx) - width, min(ay, by) - width, max(ax, bx) + width, max(ay, by) + width)
    return make


def diamond(cx, cy, r):
    return poly([(cx, cy - r), (cx + r * 0.8, cy), (cx, cy + r), (cx - r * 0.8, cy)])


# ---- 부위별 그림 ----

def cap(c, base):
    c.shape(ellipse(16, 18, 10, 10, top_only=True), base)
    c.shape(rect(4, 17, 28, 21, 1.5), shade(base, 0.8))
    c.shape(line(16, 9, 16, 17, 1.2), shade(base, 0.7), outline=False)


def helm(c, base, plume=None):
    if plume:
        c.shape(poly([(15, 3), (22, 2), (25, 7), (17, 8)]), plume)
    c.shape(rect(7, 8, 25, 28, 7), base)
    c.shape(rect(9, 16, 23, 19, 1), INK, outline=False)
    c.shape(rect(15, 19, 17, 26), shade(base, 0.75), outline=False)
    c.shape(ellipse(12, 12, 2.5, 2, ), shade(base, 1.25), outline=False)


def armor(c, base, detail):
    c.shape(poly([(9, 5), (13, 5), (16, 8), (19, 5), (23, 5), (29, 10), (26, 15), (24, 14), (24, 28), (8, 28), (8, 14), (6, 15), (3, 10)]), base)
    c.shape(poly([(13, 5), (16, 8), (19, 5), (17, 11), (15, 11)]), shade(base, 0.6), outline=False)
    if detail == "quilt":
        for y in (16, 21, 26):
            c.shape(line(9, y, 23, y, 0.8), shade(base, 0.75), outline=False)
    elif detail == "chain":
        for y in range(14, 28, 3):
            for x in range(10 + (y % 2), 24, 3):
                c.shape(ellipse(x, y, 1, 1, inner=0.4), shade(base, 0.7), outline=False)
    else:
        c.shape(line(16, 12, 16, 27, 1), shade(base, 0.7), outline=False)
        c.shape(ellipse(12, 16, 2.5, 2), shade(base, 1.3), outline=False)


def pants(c, base, belt):
    c.shape(poly([(8, 5), (24, 5), (26, 28), (19, 28), (16, 13), (13, 28), (6, 28)]), base)
    c.shape(rect(8, 5, 24, 8), belt, outline=False)
    c.shape(rect(14.5, 5.5, 17.5, 7.5), (226, 196, 106), outline=False)


def boots(c, base, trim):
    c.shape(poly([(9, 4), (18, 4), (18, 19), (27, 21), (28, 28), (7, 28), (7, 18)]), base)
    c.shape(rect(8, 4, 19, 8), trim, outline=False)
    c.shape(rect(7, 25, 28, 28), shade(base, 0.6), outline=False)
    if trim[0] > 150:
        c.shape(poly([(19, 11), (26, 8), (22, 14)]), (214, 236, 245))


def backpack(c, base, big):
    top = 6 if big else 10
    c.shape(rect(7, top, 25, 28, 4), base)
    c.shape(rect(7, top, 25, top + 8, 4), shade(base, 0.8))
    c.shape(rect(14, top + 6, 18, top + 10, 1), (226, 196, 106), outline=False)
    c.shape(rect(10, 20, 22, 26, 2), shade(base, 1.15), outline=False)
    c.shape(line(12, top, 12, top - 3, 1.4), shade(base, 0.6), outline=False)
    c.shape(line(20, top, 20, top - 3, 1.4), shade(base, 0.6), outline=False)


def pendant(c, gem):
    c.shape(line(8, 4, 16, 17, 1.2), (210, 180, 90), outline=False)
    c.shape(line(24, 4, 16, 17, 1.2), (210, 180, 90), outline=False)
    c.shape(diamond(16, 21, 7), gem)
    c.shape(diamond(14.5, 19.5, 2), shade(gem, 1.6), outline=False)


def bracelet(c, metal, gem):
    c.shape(ellipse(16, 17, 11, 8, inner=0.6), metal)
    c.shape(ellipse(16, 25, 3, 2.5), gem)


def ring(c, metal, gem):
    c.shape(ellipse(16, 20, 8, 8, inner=0.65), metal)
    c.shape(diamond(16, 10, 5), gem)
    c.shape(diamond(15, 9, 1.5), shade(gem, 1.6), outline=False)


def charm(c, paper, mark):
    c.shape(poly([(10, 4), (22, 4), (22, 26), (16, 29), (10, 26)]), paper)
    c.shape(line(16, 8, 16, 22, 1.5), mark, outline=False)
    c.shape(line(12, 12, 20, 12, 1.5), mark, outline=False)
    c.shape(line(12, 17, 20, 17, 1.5), mark, outline=False)


def earring(c, metal, gem):
    c.shape(ellipse(16, 8, 4, 4, inner=0.55), metal)
    c.shape(line(16, 12, 16, 16, 1.2), metal, outline=False)
    c.shape(poly([(16, 15), (21, 22), (16, 29), (11, 22)]), gem)
    c.shape(poly([(13, 21), (21, 18), (20, 22)]), (240, 240, 232), outline=False)


def amulet(c, metal, gem):
    c.shape(ellipse(16, 17, 11, 11), metal)
    c.shape(ellipse(16, 17, 7, 7), shade(metal, 0.7), outline=False)
    c.shape(poly([(16, 11), (21, 15), (19, 22), (13, 22), (11, 15)]), gem)


LEATHER, IRON, STEEL, GOLD = hexc("8A5A36"), hexc("8C96A0"), hexc("B8C4CE"), hexc("D9B04A")
ICONS = {
    "gear-leather-cap": lambda c: cap(c, LEATHER),
    "gear-iron-helm": lambda c: helm(c, IRON),
    "gear-knight-helm": lambda c: helm(c, STEEL, plume=hexc("C0392B")),
    "gear-padded-armor": lambda c: armor(c, hexc("B49A6A"), "quilt"),
    "gear-chain-mail": lambda c: armor(c, IRON, "chain"),
    "gear-plate-armor": lambda c: armor(c, STEEL, "plate"),
    "gear-cloth-pants": lambda c: pants(c, hexc("6F8FA8"), hexc("4A3A2A")),
    "gear-leather-pants": lambda c: pants(c, LEATHER, hexc("3B2A1E")),
    "gear-ranger-leggings": lambda c: pants(c, hexc("4E7A4A"), hexc("2F2A22")),
    "gear-leather-shoes": lambda c: boots(c, LEATHER, hexc("6B4428")),
    "gear-travel-boots": lambda c: boots(c, hexc("5E4632"), hexc("A27D4E")),
    "gear-swift-boots": lambda c: boots(c, hexc("3E6E8E"), hexc("BFE3F2")),
    "gear-small-backpack": lambda c: backpack(c, hexc("9C6B3F"), False),
    "gear-large-backpack": lambda c: backpack(c, hexc("6E7F45"), True),
    "gear-sapphire-pendant": lambda c: pendant(c, hexc("2F6FD6")),
    "gear-vigor-bracelet": lambda c: bracelet(c, hexc("C9894A"), hexc("E2A93B")),
    "gear-life-ring": lambda c: ring(c, GOLD, hexc("D23C4A")),
    "gear-mana-ring": lambda c: ring(c, hexc("C7CDD4"), hexc("3D8BE8")),
    "gear-wind-charm": lambda c: charm(c, hexc("EADFB8"), hexc("3FA07A")),
    "gear-hawk-earring": lambda c: earring(c, GOLD, hexc("E3762B")),
    "gear-guard-amulet": lambda c: amulet(c, hexc("A9B3BC"), hexc("5DBB63")),
}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    for item_id, draw in ICONS.items():
        canvas = Canvas()
        draw(canvas)
        canvas.png(os.path.join(OUT, item_id + ".png"))
        print(item_id)
