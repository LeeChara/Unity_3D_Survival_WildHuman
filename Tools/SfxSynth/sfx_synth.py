"""합성 임시 효과음 생성기 (표준 라이브러리만 사용)

실행: python -I Tools/SfxSynth/sfx_synth.py
출력: Assets/Audio/Sfx/Placeholder/Generated/<분류>/<이름>[_<번호>].wav

- 파일 이름(번호 제외)이 SfxSetting의 필드 이름, 또는 "<몬스터 데이터 이름>_<소리>" / "footstep_<바이옴 id>"
- 같은 이름에 번호가 붙은 파일들은 한 효과음의 변형 (Unity의 Tools > Sfx > Build Placeholder Cues가 하나로 묶음)
- 시드가 고정이라 다시 실행해도 같은 소리가 나옴. 수치를 고쳐 다시 뽑은 뒤 Unity에서 빌드 메뉴를 다시 실행
"""

import math
import os
import random
import struct
import wave

SR = 44100
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Audio", "Sfx", "Placeholder", "Generated")
TAU = math.pi * 2


# ---------------------------------------------------------------- 기본 신호

def n_of(sec):
    return max(1, int(sec * SR))


def silence(sec):
    return [0.0] * n_of(sec)


def noise(sec, rng):
    return [rng.uniform(-1.0, 1.0) for _ in range(n_of(sec))]


def tone(sec, freq, wave_type="sine", rng=None):
    """freq: 숫자 또는 진행도(0~1)를 받는 함수"""
    n = n_of(sec)
    out = []
    phase = 0.0
    for i in range(n):
        f = freq(i / n) if callable(freq) else freq
        phase += f / SR
        p = phase % 1.0
        if wave_type == "sine":
            v = math.sin(TAU * p)
        elif wave_type == "square":
            v = 1.0 if p < 0.5 else -1.0
        elif wave_type == "saw":
            v = 2.0 * p - 1.0
        else:  # triangle
            v = 4.0 * abs(p - 0.5) - 1.0
        out.append(v)
    return out


# ---------------------------------------------------------------- 필터

def lowpass(sig, cutoff):
    """1차 저역 통과. cutoff는 숫자 또는 진행도 함수"""
    out = []
    y = 0.0
    n = len(sig)
    for i, x in enumerate(sig):
        c = cutoff(i / n) if callable(cutoff) else cutoff
        a = 1.0 - math.exp(-TAU * max(c, 1.0) / SR)
        y += a * (x - y)
        out.append(y)
    return out


def highpass(sig, cutoff):
    low = lowpass(sig, cutoff)
    return [x - l for x, l in zip(sig, low)]


def bandpass(sig, center, q=1.0):
    """2차 대역 통과 (RBJ). center는 숫자 또는 진행도 함수"""
    out = []
    x1 = x2 = y1 = y2 = 0.0
    n = len(sig)
    for i, x in enumerate(sig):
        f = center(i / n) if callable(center) else center
        f = min(max(f, 20.0), SR * 0.45)
        w = TAU * f / SR
        alpha = math.sin(w) / (2 * q)
        cw = math.cos(w)
        a0 = 1 + alpha
        b0 = alpha / a0
        b2 = -alpha / a0
        a1 = -2 * cw / a0
        a2 = (1 - alpha) / a0
        y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, x
        y2, y1 = y1, y
        out.append(y)
    return out


# ---------------------------------------------------------------- 엔벨로프·조합

def env(sig, attack=0.005, decay=0.1, curve=4.0):
    """빠른 상승 후 지수 감쇠 (decay: 대략 소리가 사라지는 시간)"""
    n = len(sig)
    a = n_of(attack)
    out = []
    for i in range(n):
        if i < a:
            g = i / a
        else:
            t = (i - a) / SR
            g = math.exp(-curve * t / max(decay, 1e-4))
        out.append(sig[i] * g)
    return out


def shape(sig, points):
    """구간 선형 엔벨로프. points: [(진행도, 크기), ...]"""
    n = len(sig)
    out = []
    j = 0
    for i in range(n):
        p = i / n
        while j < len(points) - 2 and p > points[j + 1][0]:
            j += 1
        (p0, g0), (p1, g1) = points[j], points[j + 1]
        g = g0 if p1 == p0 else g0 + (g1 - g0) * min(max((p - p0) / (p1 - p0), 0.0), 1.0)
        out.append(sig[i] * g)
    return out


def gain(sig, g):
    return [x * g for x in sig]


def mix(*parts):
    """parts: 신호 또는 (신호, 시작 초, 크기)"""
    items = []
    length = 0
    for part in parts:
        if isinstance(part, tuple):
            sig, start, g = part
        else:
            sig, start, g = part, 0.0, 1.0
        s = n_of(start) if start > 0 else 0
        items.append((sig, s, g))
        length = max(length, s + len(sig))
    out = [0.0] * length
    for sig, s, g in items:
        for i, x in enumerate(sig):
            out[s + i] += x * g
    return out


def crackle(sec, rng, rate, sharp=0.002):
    """무작위 딱딱 소리 (rate: 초당 개수)"""
    out = [0.0] * n_of(sec)
    count = int(sec * rate)
    for _ in range(count):
        at = rng.randrange(len(out))
        amp = rng.uniform(0.3, 1.0)
        length = n_of(rng.uniform(sharp, sharp * 4))
        for k in range(length):
            if at + k >= len(out):
                break
            out[at + k] += rng.uniform(-1, 1) * amp * (1 - k / length)
    return out


def tremolo(sig, rate, depth, phase=0.0):
    return [x * (1 - depth + depth * (0.5 + 0.5 * math.sin(TAU * (rate * i / SR + phase)))) for i, x in enumerate(sig)]


def random_am(sig, rng, rate, depth):
    """느리게 흔들리는 무작위 크기 변화 (바람, 천둥 등)"""
    step = max(1, int(SR / rate))
    targets = [rng.uniform(1 - depth, 1) for _ in range(len(sig) // step + 2)]
    out = []
    for i, x in enumerate(sig):
        k = i // step
        f = (i % step) / step
        f = f * f * (3 - 2 * f)
        out.append(x * (targets[k] + (targets[k + 1] - targets[k]) * f))
    return out


def normalize(sig, peak=0.9):
    m = max((abs(x) for x in sig), default=0.0)
    if m <= 1e-9:
        return sig
    return [x * peak / m for x in sig]


def fade_edges(sig, fade_in=0.002, fade_out=0.01):
    n = len(sig)
    a = min(n_of(fade_in), n // 2)
    b = min(n_of(fade_out), n // 2)
    out = list(sig)
    for i in range(a):
        out[i] *= i / a
    for i in range(b):
        out[n - 1 - i] *= i / b
    return out


def make_loop(sig, cross=0.5):
    """끝 cross초를 앞부분에 겹쳐 이음매 없이 반복되게 함 (결과는 cross만큼 짧아짐)"""
    c = n_of(cross)
    body = sig[:-c]
    tail = sig[-c:]
    out = list(body)
    for i in range(c):
        f = i / c
        out[i] = out[i] * f + tail[i] * (1 - f)
    return out


def sweep(a, b, power=1.0):
    return lambda p: a + (b - a) * (p ** power)


# ---------------------------------------------------------------- 자주 쓰는 소리 조각

def thump(rng, freq=90, sec=0.15, drop=0.5):
    return env(tone(sec, sweep(freq, freq * drop)), 0.002, sec * 0.6)


def knock(rng, freq, sec=0.15, q=8):
    body = bandpass(noise(sec, rng), freq, q)
    ring = tone(sec, freq * 0.55)
    click = highpass(noise(0.01, rng), 2000)
    return env(mix((normalize(body), 0, 0.8), (ring, 0, 0.4), (click, 0, 0.5)), 0.001, sec * 0.5)


def whoosh(rng, sec, f0, f1, f2, q=1.2):
    def center(p):
        return f0 + (f1 - f0) * (p * 2) if p < 0.5 else f1 + (f2 - f1) * ((p - 0.5) * 2)
    sig = bandpass(noise(sec, rng), center, q)
    return shape(normalize(sig), [(0, 0), (0.45, 1), (1, 0)])


def chime(freq, sec, partials=((1, 1.0), (2.01, 0.35), (3.02, 0.12)), decay=None):
    parts = [gain(tone(sec, freq * r), a) for r, a in partials]
    return env(mix(*parts), 0.003, decay or sec * 0.7)


def splash(rng, sec=0.4, center=1400):
    body = bandpass(noise(sec, rng), sweep(center * 1.6, center * 0.6), 0.8)
    bubbles = silence(sec)
    for _ in range(6):
        b = env(tone(rng.uniform(0.03, 0.06), sweep(rng.uniform(300, 600), rng.uniform(900, 1600))), 0.002, 0.03)
        bubbles = mix(bubbles, (b, rng.uniform(0.05, sec * 0.7), rng.uniform(0.2, 0.4)))
    return env(mix((normalize(body), 0, 1), bubbles), 0.01, sec * 0.5, 3)


def growl(rng, sec, freq, rough=28, cutoff=700):
    sig = tone(sec, lambda p: freq * (1 + 0.08 * math.sin(TAU * 3 * p)), "saw")
    sig = tremolo(sig, rough, 0.6)
    sig = mix((lowpass(sig, cutoff), 0, 1), (bandpass(noise(sec, rng), cutoff, 1.5), 0, 0.3))
    return shape(normalize(sig), [(0, 0), (0.15, 1), (0.7, 0.8), (1, 0)])


def squeal(rng, sec, f0, f1):
    sig = tone(sec, sweep(f0, f1, 0.7), "saw")
    sig = bandpass(sig, sweep(f0 * 2.5, f1 * 2.5), 2.0)
    return env(normalize(sig), 0.005, sec * 0.7, 3)


def crunch(rng, sec, center, rate=900):
    sig = crackle(sec, rng, rate, 0.0015)
    sig = bandpass(sig, center, 0.9)
    return env(normalize(sig), 0.003, sec * 0.6)


def rustle(rng, sec, center=3200, rate=600):
    base = bandpass(noise(sec, rng), center, 0.7)
    crack = bandpass(crackle(sec, rng, rate, 0.001), center * 1.2, 1.0)
    sig = mix((normalize(base), 0, 0.6), (normalize(crack), 0, 0.7))
    return shape(sig, [(0, 0), (0.1, 1), (0.5, 0.6), (1, 0)])


# ---------------------------------------------------------------- 효과음 정의
# 각 함수는 rng를 받아 신호 하나를 돌려줌. variants 개수만큼 다른 시드로 만들어 번호를 붙임

SOUNDS = {}


def sound(category, name, variants=1, peak=0.85, loop=False):
    def register(fn):
        SOUNDS[name] = (category, fn, variants, peak, loop)
        return fn
    return register


# 플레이어 ----------------------------------------------------------

@sound("Player", "footstep", 4, 0.6)
def _(rng):
    return mix(env(lowpass(noise(0.1, rng), 900), 0.002, 0.05), (thump(rng, 80, 0.08), 0, 0.7))


@sound("Player", "footstep_forest", 4, 0.55)
def _(rng):
    grass = env(bandpass(noise(0.12, rng), rng.uniform(1600, 2200), 0.8), 0.004, 0.06)
    return mix((normalize(grass), 0, 0.8), (thump(rng, 75, 0.08), 0, 0.5))


@sound("Player", "footstep_desert", 4, 0.5)
def _(rng):
    sand = env(lowpass(highpass(noise(0.16, rng), 400), 3500), 0.02, 0.09)
    return mix((normalize(sand), 0, 0.8), (thump(rng, 70, 0.07), 0, 0.3))


@sound("Player", "footstep_snow", 4, 0.55)
def _(rng):
    return mix((crunch(rng, 0.18, rng.uniform(2200, 2800), 1400), 0, 0.9), (thump(rng, 70, 0.08), 0, 0.3))


@sound("Player", "footstep_volcanic", 4, 0.55)
def _(rng):
    gravel = bandpass(crackle(0.12, rng, 500, 0.002), 1800, 0.7)
    return mix((env(normalize(gravel), 0.002, 0.07), 0, 0.8), (thump(rng, 85, 0.08), 0, 0.6))


@sound("Player", "footstepWater", 4, 0.5)
def _(rng):
    return splash(rng, 0.22, rng.uniform(1000, 1400))


@sound("Player", "footstepIce", 4, 0.45)
def _(rng):
    click = env(highpass(noise(0.03, rng), 3000), 0.001, 0.015)
    ring = chime(rng.uniform(2600, 3200), 0.12, ((1, 1), (1.7, 0.4)), 0.05)
    scrape = env(bandpass(noise(0.15, rng), 5000, 2), 0.02, 0.1)
    return mix((normalize(click), 0, 0.7), (ring, 0, 0.3), (normalize(scrape), 0.01, 0.3))


@sound("Player", "swing", 3, 0.6)
def _(rng):
    return whoosh(rng, 0.22, 500, rng.uniform(1600, 2000), 700)


@sound("Player", "swingHeavy", 3, 0.7)
def _(rng):
    return whoosh(rng, 0.32, 250, rng.uniform(700, 900), 300, 1.0)


@sound("Player", "dodge", 2, 0.6)
def _(rng):
    cloth = whoosh(rng, 0.28, 300, 1100, 400, 0.8)
    return mix((cloth, 0, 0.8), (thump(rng, 70, 0.12), 0.18, 0.6))


@sound("Player", "playerHurt", 3, 0.75)
def _(rng):
    voice = bandpass(tone(0.2, sweep(rng.uniform(230, 260), 150), "saw"), 750, 2)
    hit = env(lowpass(noise(0.06, rng), 2500), 0.001, 0.03)
    return mix((env(normalize(voice), 0.005, 0.12), 0, 0.8), (normalize(hit), 0, 0.6), (thump(rng, 110, 0.1), 0, 0.5))


@sound("Player", "playerDeath", 1, 0.8)
def _(rng):
    voice = bandpass(tone(0.9, sweep(300, 80, 0.6), "saw"), sweep(800, 300), 2)
    body = env(lowpass(noise(0.4, rng), 600), 0.003, 0.25)
    return mix((shape(normalize(voice), [(0, 0), (0.05, 1), (0.6, 0.6), (1, 0)]), 0, 0.8), (normalize(body), 0.5, 0.6), (thump(rng, 70, 0.3), 0.5, 0.8))


@sound("Player", "respawn", 1, 0.6)
def _(rng):
    notes = [523.25, 659.25, 783.99, 1046.5]
    return mix(*[(chime(f, 0.8), i * 0.09, 0.6) for i, f in enumerate(notes)])


@sound("Player", "playerStun", 1, 0.5)
def _(rng):
    sig = mix(tone(0.7, lambda p: 1500 + 300 * math.sin(TAU * 7 * p * 0.7)), (tone(0.7, lambda p: 1900 + 300 * math.sin(TAU * 7 * p * 0.7 + 1.5)), 0, 0.6))
    return shape(sig, [(0, 0), (0.05, 1), (0.7, 0.6), (1, 0)])


@sound("Player", "eat", 2, 0.65)
def _(rng):
    parts = [(crunch(rng, 0.12, rng.uniform(1500, 2200), 1200), i * 0.15 + rng.uniform(0, 0.03), rng.uniform(0.7, 1)) for i in range(3)]
    return mix(*parts)


@sound("Player", "pickup", 1, 0.5)
def _(rng):
    return env(tone(0.09, sweep(600, 1300, 0.5)), 0.002, 0.06)


@sound("Player", "throwItem", 2, 0.5)
def _(rng):
    return whoosh(rng, 0.16, 700, 2200, 900, 1.5)


@sound("Player", "place", 2, 0.7)
def _(rng):
    return mix((knock(rng, 300, 0.18, 4), 0, 0.8), (thump(rng, 100, 0.15), 0, 0.8))


@sound("Player", "bucketScoop", 1, 0.6)
def _(rng):
    gurgle = silence(0.5)
    for _ in range(10):
        b = env(tone(rng.uniform(0.03, 0.05), sweep(rng.uniform(250, 450), rng.uniform(700, 1100))), 0.003, 0.03)
        gurgle = mix(gurgle, (b, rng.uniform(0.05, 0.4), rng.uniform(0.3, 0.6)))
    return mix((splash(rng, 0.3, 900), 0, 0.6), gurgle)


@sound("Player", "bucketPour", 1, 0.7)
def _(rng):
    return mix((splash(rng, 0.6, 1600), 0, 1), (splash(rng, 0.4, 900), 0.08, 0.6))


@sound("Player", "hungerWarning", 1, 0.6)
def _(rng):
    sig = tone(0.8, lambda p: 65 + 20 * math.sin(TAU * 2.5 * p), "saw")
    sig = tremolo(lowpass(sig, 300), 11, 0.7)
    return shape(normalize(sig), [(0, 0), (0.2, 1), (0.5, 0.5), (0.7, 0.9), (1, 0)])


@sound("Player", "heartbeat", 1, 0.7)
def _(rng):
    return mix((thump(rng, 60, 0.14, 0.6), 0, 1), (thump(rng, 55, 0.14, 0.6), 0.16, 0.7))


@sound("Player", "equipOn", 2, 0.55)
def _(rng):
    cloth = whoosh(rng, 0.14, 600, 1800, 900, 1.2)
    clink = chime(rng.uniform(1800, 2200), 0.2, ((1, 1), (2.7, 0.4)), 0.06)
    return mix((cloth, 0, 0.6), (clink, 0.08, 0.5), (thump(rng, 150, 0.06), 0.08, 0.4))


@sound("Player", "equipOff", 2, 0.5)
def _(rng):
    cloth = whoosh(rng, 0.16, 1500, 900, 400, 1.2)
    return mix((cloth, 0, 0.7), (thump(rng, 120, 0.06), 0.1, 0.4))


@sound("Player", "hotbarSelect", 1, 0.3)
def _(rng):
    return mix(env(tone(0.03, 1400, "triangle"), 0.001, 0.012), (env(highpass(noise(0.01, rng), 3000), 0.0005, 0.005), 0, 0.3))


# 채집 ----------------------------------------------------------

@sound("Gather", "hitWood", 3, 0.75)
def _(rng):
    return knock(rng, rng.uniform(380, 460), 0.16, 6)


@sound("Gather", "breakWood", 1, 0.85)
def _(rng):
    creak = bandpass(tone(0.55, lambda p: 110 + 40 * p + 15 * math.sin(TAU * 9 * p), "saw"), 900, 3)
    creak = shape(normalize(creak), [(0, 0), (0.2, 0.7), (0.9, 1), (1, 0)])
    crash = env(lowpass(noise(0.9, rng), sweep(2500, 400)), 0.005, 0.5, 3)
    twigs = env(bandpass(crackle(0.6, rng, 300, 0.002), 2500, 0.8), 0.005, 0.4, 3)
    return mix((creak, 0, 0.5), (normalize(crash), 0.45, 0.8), (normalize(twigs), 0.45, 0.4), (thump(rng, 60, 0.35), 0.5, 1))


@sound("Gather", "hitStone", 3, 0.75)
def _(rng):
    click = env(highpass(noise(0.02, rng), 2500), 0.0005, 0.01)
    ring = env(bandpass(noise(0.12, rng), rng.uniform(1800, 2400), 12), 0.001, 0.05)
    return mix((normalize(click), 0, 0.7), (normalize(ring), 0, 0.7), (thump(rng, 160, 0.05), 0, 0.4))


@sound("Gather", "breakStone", 1, 0.85)
def _(rng):
    rubble = silence(0.7)
    for _ in range(14):
        piece = env(bandpass(noise(0.05, rng), rng.uniform(900, 2600), 5), 0.001, 0.03)
        rubble = mix(rubble, (normalize(piece), rng.uniform(0, 0.5), rng.uniform(0.3, 0.8)))
    return mix(rubble, (thump(rng, 90, 0.3), 0, 1), (env(lowpass(noise(0.4, rng), 800), 0.002, 0.2), 0, 0.5))


@sound("Gather", "hitPlant", 3, 0.6)
def _(rng):
    return rustle(rng, 0.16, rng.uniform(2800, 3600))


@sound("Gather", "breakPlant", 1, 0.7)
def _(rng):
    return mix(rustle(rng, 0.45, 3000, 900), (rustle(rng, 0.3, 2200), 0.1, 0.6))


@sound("Gather", "hitBlocked", 2, 0.55)
def _(rng):
    return chime(rng.uniform(2300, 2600), 0.25, ((1, 1), (1.47, 0.6), (2.76, 0.3)), 0.08)


@sound("Gather", "hitStructure", 3, 0.7)
def _(rng):
    return knock(rng, rng.uniform(260, 320), 0.18, 5)


@sound("Gather", "breakStructure", 1, 0.85)
def _(rng):
    planks = silence(0.6)
    for _ in range(5):
        planks = mix(planks, (knock(rng, rng.uniform(200, 400), 0.15, 4), rng.uniform(0, 0.35), rng.uniform(0.4, 0.8)))
    return mix(planks, (thump(rng, 70, 0.3), 0, 1), (env(lowpass(noise(0.5, rng), 1200), 0.003, 0.3), 0, 0.4))


@sound("Gather", "prick", 2, 0.5)
def _(rng):
    scratch = env(highpass(noise(0.06, rng), 4500), 0.001, 0.03)
    return mix((normalize(scratch), 0, 0.8), (env(tone(0.05, 2200), 0.001, 0.02), 0, 0.3))


# 몬스터 (기본) ----------------------------------------------------------

@sound("Monster", "monsterHurt", 3, 0.7)
def _(rng):
    return mix((squeal(rng, 0.18, rng.uniform(480, 560), 340), 0, 0.8), (thump(rng, 120, 0.08), 0, 0.6))


@sound("Monster", "monsterDeath", 2, 0.75)
def _(rng):
    return mix((squeal(rng, 0.55, rng.uniform(420, 480), 110), 0, 0.9), (thump(rng, 70, 0.25), 0.3, 0.7))


@sound("Monster", "monsterDetect", 2, 0.6)
def _(rng):
    a = env(tone(0.08, sweep(450, 750), "triangle"), 0.003, 0.05)
    b = env(tone(0.1, sweep(500, 900), "triangle"), 0.003, 0.07)
    return mix(a, (b, 0.1, 1))


@sound("Monster", "monsterWindup", 2, 0.65)
def _(rng):
    return growl(rng, 0.5, rng.uniform(85, 100))


@sound("Monster", "monsterAttack", 2, 0.6)
def _(rng):
    snap = env(bandpass(noise(0.05, rng), 1500, 2), 0.001, 0.02)
    return mix((whoosh(rng, 0.25, 400, 1400, 500), 0, 0.8), (normalize(snap), 0.15, 0.6))


@sound("Monster", "monsterStun", 1, 0.45)
def _(rng):
    return SOUNDS["playerStun"][1](rng)


# 몬스터 (종별: "<MonsterData 에셋 이름>_<detect|windup|attack|hurt|death>") -----------------

@sound("Monster", "Roseviper_detect", 1, 0.55)
def _(rng):
    dirt = lowpass(crackle(0.5, rng, 700, 0.002), 1200)
    return shape(normalize(mix(dirt, (lowpass(noise(0.5, rng), 400), 0, 0.5))), [(0, 0), (0.3, 1), (1, 0)])


@sound("Monster", "Roseviper_attack", 2, 0.6)
def _(rng):
    hiss = highpass(noise(0.4, rng), 3500)
    return mix((shape(normalize(hiss), [(0, 0), (0.1, 1), (1, 0)]), 0, 0.7), (whoosh(rng, 0.2, 600, 2000, 800), 0.05, 0.6))


@sound("Monster", "Roseviper_hurt", 2, 0.6)
def _(rng):
    hiss = env(highpass(noise(0.2, rng), 2500), 0.003, 0.12)
    return mix((normalize(hiss), 0, 0.6), (squeal(rng, 0.15, 700, 450), 0, 0.6))


@sound("Monster", "Shieldboar_windup", 2, 0.7)
def _(rng):
    snorts = silence(0.6)
    for i in range(3):
        s = env(bandpass(noise(0.12, rng), rng.uniform(500, 700), 1.5), 0.01, 0.07)
        snorts = mix(snorts, (normalize(s), i * 0.17, 0.8))
    return mix(snorts, (growl(rng, 0.5, 70, 22, 500), 0, 0.5))


@sound("Monster", "Shieldboar_attack", 2, 0.7)
def _(rng):
    hooves = silence(0.4)
    for i in range(4):
        hooves = mix(hooves, (thump(rng, rng.uniform(90, 120), 0.07), i * 0.08, 0.7))
    return mix(hooves, (whoosh(rng, 0.35, 200, 700, 250, 0.8), 0, 0.6))


@sound("Monster", "Shieldboar_attackLoop", 1, 0.6, loop=True)
def _(rng):
    sec = 2.5
    hooves = silence(sec)
    t = 0.0
    while t < sec - 0.1:
        hooves = mix(hooves, (thump(rng, rng.uniform(85, 115), 0.08), t, rng.uniform(0.6, 1.0)))
        hooves = mix(hooves, (env(lowpass(noise(0.05, rng), 1500), 0.002, 0.03), t, 0.3))
        t += rng.uniform(0.11, 0.15)
    return hooves


@sound("Monster", "Shieldboar_hurt", 2, 0.7)
def _(rng):
    return mix((squeal(rng, 0.2, 300, 200), 0, 0.8), (thump(rng, 100, 0.1), 0, 0.7))


@sound("Monster", "Stoneotter_windup", 2, 0.55)
def _(rng):
    chit = silence(0.4)
    for i in range(5):
        chit = mix(chit, (env(tone(0.04, sweep(1100, 1500), "triangle"), 0.002, 0.03), i * 0.07, 0.8))
    return chit


@sound("Monster", "Stoneotter_attack", 2, 0.5)
def _(rng):
    return whoosh(rng, 0.15, 800, 2500, 1000, 1.5)


@sound("Monster", "Stoneotter_hurt", 2, 0.65)
def _(rng):
    return squeal(rng, 0.15, 800, 550)


@sound("Monster", "Wingsquirrel_detect", 2, 0.55)
def _(rng):
    return env(tone(0.12, sweep(1800, 2600), "triangle"), 0.003, 0.08)


@sound("Monster", "Wingsquirrel_attack", 2, 0.55)
def _(rng):
    return whoosh(rng, 0.5, 400, 1300, 600, 0.9)


@sound("Monster", "Wingsquirrel_hurt", 2, 0.6)
def _(rng):
    return squeal(rng, 0.14, 1100, 800)


# 불·물 ----------------------------------------------------------

@sound("Fire", "ignite", 2, 0.75)
def _(rng):
    whomp = lowpass(noise(0.45, rng), sweep(200, 3000, 0.5))
    return mix((shape(normalize(whomp), [(0, 0), (0.15, 1), (1, 0)]), 0, 0.8), (thump(rng, 70, 0.2), 0, 0.5))


def fire_loop(rng, sec, rumble_cut, crackle_rate, crackle_gain):
    rumble = random_am(lowpass(noise(sec, rng), rumble_cut), rng, 3, 0.5)
    crack = bandpass(crackle(sec, rng, crackle_rate, 0.0015), 2500, 0.7)
    return mix((normalize(rumble), 0, 0.6), (normalize(crack), 0, crackle_gain))


@sound("Fire", "burnLoop", 1, 0.6, loop=True)
def _(rng):
    return fire_loop(rng, 3.5, 450, 40, 0.8)


@sound("Fire", "extinguish", 2, 0.6)
def _(rng):
    hiss = highpass(noise(0.8, rng), sweep(2500, 5000))
    return shape(normalize(hiss), [(0, 0), (0.05, 1), (0.4, 0.5), (1, 0)])


@sound("Fire", "splash", 2, 0.7)
def _(rng):
    return splash(rng, 0.4, 1300)


@sound("Fire", "fuseLoop", 1, 0.5, loop=True)
def _(rng):
    sizzle = highpass(noise(2.0, rng), 3500)
    sizzle = random_am(sizzle, rng, 30, 0.7)
    pops = highpass(crackle(2.0, rng, 25, 0.001), 2000)
    return mix((normalize(sizzle), 0, 0.7), (normalize(pops), 0, 0.5))


@sound("Fire", "explosion", 2, 0.95)
def _(rng):
    boom = env(lowpass(noise(1.6, rng), sweep(1800, 120, 0.4)), 0.002, 0.8, 3)
    sub = env(tone(1.0, sweep(70, 30)), 0.002, 0.6)
    debris = env(bandpass(crackle(1.2, rng, 120, 0.003), 1500, 0.7), 0.05, 0.8, 3)
    return mix((normalize(boom), 0, 1), (sub, 0, 0.9), (normalize(debris), 0.1, 0.35))


# 보스 (태양곰: "SunBear_<소리>"는 BossData, "<패턴 에셋 이름>_<소리>"는 패턴) ---------------

def roar(rng, sec, f0, f1, cutoff=900):
    voice = tone(sec, lambda p: (f0 + (f1 - f0) * p) * (1 + 0.04 * math.sin(TAU * 6 * p * sec)), "saw")
    voice = tremolo(voice, 32, 0.45)
    body = mix((bandpass(voice, cutoff * 0.6, 1.5), 0, 0.8), (bandpass(voice, cutoff, 2.0), 0, 0.6), (lowpass(voice, 300), 0, 0.6))
    breath = bandpass(noise(sec, rng), cutoff * 1.4, 0.7)
    sig = mix((normalize(body), 0, 1), (normalize(breath), 0, 0.35))
    return shape(normalize(sig), [(0, 0), (0.12, 1), (0.7, 0.85), (1, 0)])


def boom(rng, sec=1.0, low=60):
    hit = env(lowpass(noise(sec, rng), sweep(1500, 100, 0.4)), 0.002, sec * 0.5, 3)
    sub = env(tone(sec * 0.8, sweep(low, low * 0.5)), 0.002, sec * 0.5)
    debris = env(bandpass(crackle(sec * 0.8, rng, 90, 0.003), 1200, 0.7), 0.03, sec * 0.5, 3)
    return mix((normalize(hit), 0, 0.9), (sub, 0, 1.0), (normalize(debris), 0.05, 0.3))


def heavy_whoosh(rng, sec=0.35):
    return whoosh(rng, sec, 180, rng.uniform(600, 800), 220, 0.8)


def gallop(rng, sec, freq, gap, thump_sec):
    steps = silence(sec)
    t = 0.0
    while t < sec - 0.1:
        steps = mix(steps, (thump(rng, rng.uniform(freq * 0.85, freq * 1.1), thump_sec), t, rng.uniform(0.7, 1.0)))
        steps = mix(steps, (env(lowpass(noise(0.08, rng), 900), 0.002, 0.05), t, 0.4))
        t += rng.uniform(gap * 0.9, gap * 1.1)
    return steps


@sound("Boss", "SunBear_detect", 1, 0.8)
def _(rng):
    return growl(rng, 0.9, 60, 24, 500)


@sound("Boss", "SunBear_hurt", 3, 0.8)
def _(rng):
    return mix((roar(rng, 0.3, rng.uniform(150, 170), 110, 700), 0, 0.8), (thump(rng, 80, 0.12), 0, 0.6))


@sound("Boss", "SunBear_death", 1, 0.9)
def _(rng):
    return mix((roar(rng, 2.2, 150, 50, 700), 0, 1), (boom(rng, 1.2, 50), 1.6, 0.8))


@sound("Boss", "SunBear_phase", 1, 0.9)
def _(rng):
    whomp = lowpass(noise(1.2, rng), sweep(200, 4000, 0.4))
    return mix((shape(normalize(whomp), [(0, 0), (0.2, 1), (1, 0)]), 0, 0.8), (fire_loop(rng, 1.2, 500, 60, 0.8), 0.2, 0.5), (thump(rng, 50, 0.5), 0, 0.8))


@sound("Boss", "SunBear_fireRingLoop", 1, 0.55, loop=True)
def _(rng):
    return fire_loop(rng, 4.5, 350, 30, 0.7)


@sound("Boss", "SunBear_summon", 1, 0.75)
def _(rng):
    hum = mix(*[(env(tone(2.2, f), 0.6, 1.6, 2), 0, 0.3) for f in [110, 165, 220.5, 330]])
    rumble = random_am(lowpass(noise(2.2, rng), 120), rng, 3, 0.6)
    rumble = shape(normalize(rumble), [(0, 0), (0.5, 1), (1, 0)])
    sparkle = mix(*[(chime(f, 0.8), 0.4 + i * 0.2, 0.25) for i, f in enumerate([880, 1108.7, 1318.5])])
    return mix(hum, (rumble, 0, 0.7), sparkle)


@sound("Boss", "SunBearRoar_windup", 1, 0.7)
def _(rng):
    inhale = bandpass(noise(0.6, rng), sweep(500, 1500), 1.0)
    return mix((shape(normalize(inhale), [(0, 0), (0.8, 1), (1, 0)]), 0, 0.5), (growl(rng, 0.6, 55, 20, 400), 0, 0.7))


@sound("Boss", "SunBearRoar_attack", 1, 0.95)
def _(rng):
    return mix((roar(rng, 1.8, 120, 90, 900), 0, 1), (roar(rng, 1.8, 180, 140, 1300), 0, 0.4), (thump(rng, 45, 0.6), 0, 0.7))


@sound("Boss", "SunBearClawCombo_windup", 2, 0.5)
def _(rng):
    return growl(rng, 0.25, rng.uniform(80, 95), 30, 600)


@sound("Boss", "SunBearClawCombo_attack", 3, 0.75)
def _(rng):
    tear = env(bandpass(noise(0.12, rng), 2500, 1.5), 0.002, 0.06)
    return mix((heavy_whoosh(rng, 0.3), 0, 0.9), (normalize(tear), 0.15, 0.4))


@sound("Boss", "SunBearClawCombo_finisher", 1, 0.85)
def _(rng):
    return mix((heavy_whoosh(rng, 0.4), 0, 0.9), (heavy_whoosh(rng, 0.4), 0.03, 0.6), (thump(rng, 60, 0.3), 0.25, 0.9), (roar(rng, 0.4, 150, 120), 0, 0.4))


@sound("Boss", "SunBearGroundSlam_windup", 1, 0.7)
def _(rng):
    rise = whoosh(rng, 1.0, 150, 500, 700, 0.7)
    return mix((rise, 0, 0.5), (growl(rng, 1.0, 50, 18, 450), 0, 0.8))


@sound("Boss", "SunBearGroundSlam_attack", 2, 0.95)
def _(rng):
    return boom(rng, 1.3, 55)


@sound("Boss", "SunBearGroundSlam_followUp", 1, 0.85)
def _(rng):
    return boom(rng, 0.9, 75)


@sound("Boss", "SunBearLeap_windup", 1, 0.65)
def _(rng):
    return growl(rng, 0.55, 70, 26, 500)


@sound("Boss", "SunBearLeap_jump", 1, 0.7)
def _(rng):
    return mix((whoosh(rng, 0.6, 200, 900, 400, 0.8), 0, 0.8), (thump(rng, 70, 0.15), 0, 0.7))


@sound("Boss", "SunBearLeap_attack", 1, 0.95)
def _(rng):
    return boom(rng, 1.2, 50)


@sound("Boss", "SunBearCharge_windup", 1, 0.75)
def _(rng):
    stomps = mix((thump(rng, 70, 0.2), 0, 1), (thump(rng, 65, 0.2), 0.35, 1))
    snort = env(bandpass(noise(0.25, rng), 500, 1.2), 0.01, 0.15)
    return mix(stomps, (normalize(snort), 0.6, 0.7))


@sound("Boss", "SunBearCharge_attack", 1, 0.8)
def _(rng):
    return roar(rng, 0.5, 170, 140, 900)


@sound("Boss", "SunBearCharge_runLoop", 1, 0.7, loop=True)
def _(rng):
    return gallop(rng, 2.5, 62, 0.22, 0.15)


@sound("Boss", "SunBearCharge_crash", 1, 0.95)
def _(rng):
    crack = env(highpass(noise(0.2, rng), 1200), 0.001, 0.1)
    return mix(boom(rng, 1.0, 60), (normalize(crack), 0, 0.5))


@sound("Boss", "SunBearGrab_windup", 1, 0.6)
def _(rng):
    return mix((growl(rng, 0.5, 85, 26, 600), 0, 0.7), (whoosh(rng, 0.4, 200, 600, 400), 0.1, 0.4))


@sound("Boss", "SunBearGrab_attack", 1, 0.8)
def _(rng):
    return mix((thump(rng, 90, 0.2), 0, 1), (env(lowpass(noise(0.15, rng), 1500), 0.002, 0.08), 0, 0.5), (growl(rng, 0.6, 75, 22, 500), 0.05, 0.7))


@sound("Boss", "SunBearGrab_throw", 1, 0.8)
def _(rng):
    return mix((heavy_whoosh(rng, 0.45), 0, 1), (roar(rng, 0.4, 160, 130), 0, 0.5))


@sound("Boss", "SunBearGrab_miss", 1, 0.6)
def _(rng):
    return heavy_whoosh(rng, 0.3)


@sound("Boss", "SunBearRockThrow_windup", 1, 0.8)
def _(rng):
    rumble = random_am(lowpass(noise(0.9, rng), 200), rng, 8, 0.6)
    cracks = bandpass(crackle(0.9, rng, 40, 0.003), 1400, 0.8)
    return mix((shape(normalize(rumble), [(0, 0), (0.3, 1), (1, 0.6)]), 0, 0.8), (normalize(cracks), 0, 0.5), (growl(rng, 0.9, 60, 20, 400), 0, 0.4))


@sound("Boss", "SunBearRockThrow_attack", 1, 0.8)
def _(rng):
    return mix((heavy_whoosh(rng, 0.5), 0, 1), (roar(rng, 0.3, 170, 150), 0, 0.4))


@sound("Boss", "SunBearRockThrow_land", 1, 0.95)
def _(rng):
    return mix(boom(rng, 1.0, 65), (SOUNDS["breakStone"][1](rng), 0.02, 0.7))


# 몬스터 울음(call: 평소 가끔)·포효(roar: 공격 시작) ---------------------------------

def grunt(rng, sec, freq):
    voice = tone(sec, sweep(freq * 1.1, freq * 0.8), "saw")
    voice = tremolo(voice, rng.uniform(35, 45), 0.5)
    nasal = mix((bandpass(voice, 450, 3), 0, 0.8), (bandpass(voice, 1100, 4), 0, 0.5), (lowpass(voice, 250), 0, 0.5))
    breath = bandpass(noise(sec, rng), 700, 1.2)
    sig = mix((normalize(nasal), 0, 1), (normalize(breath), 0, 0.25))
    return env(normalize(sig), 0.01, sec * 0.7, 3)


def chirp(rng, sec, f0, f1, wave_type="sine"):
    def freq(p):
        return f0 + (f1 - f0) * math.sin(math.pi * p)
    return env(tone(sec, freq, wave_type), 0.004, sec * 0.7, 3)


@sound("Monster", "Shieldboar_call", 3, 0.6)
def _(rng):
    out = silence(0.9)
    t = 0.0
    for _ in range(rng.randint(2, 4)):
        sec = rng.uniform(0.1, 0.18)
        out = mix(out, (grunt(rng, sec, rng.uniform(95, 130)), t, rng.uniform(0.7, 1.0)))
        t += sec + rng.uniform(0.05, 0.12)
    return out


@sound("Monster", "Shieldboar_roar", 2, 0.8)
def _(rng):
    sec = 0.7
    voice = tone(sec, lambda p: 520 + 600 * math.sin(math.pi * min(p * 1.4, 1.0)) * (1 - 0.3 * p), "saw")
    voice = tremolo(voice, 30, 0.35)
    body = mix((bandpass(voice, 1500, 2), 0, 1), (bandpass(voice, 800, 2), 0, 0.6))
    snort = env(bandpass(noise(0.2, rng), 600, 1.2), 0.005, 0.1)
    return mix((shape(normalize(body), [(0, 0), (0.08, 1), (0.6, 0.8), (1, 0)]), 0.08, 0.9), (normalize(snort), 0, 0.6))


@sound("Monster", "Stoneotter_call", 3, 0.5)
def _(rng):
    out = silence(0.7)
    t = 0.0
    for _ in range(rng.randint(2, 3)):
        sec = rng.uniform(0.08, 0.14)
        base = rng.uniform(1700, 2200)
        out = mix(out, (chirp(rng, sec, base, base * rng.uniform(1.2, 1.4)), t, rng.uniform(0.7, 1.0)))
        t += sec + rng.uniform(0.04, 0.1)
    return out


@sound("Monster", "Stoneotter_roar", 2, 0.65)
def _(rng):
    voice = tone(0.35, sweep(1200, 1800, 0.5), "saw")
    return env(normalize(bandpass(voice, 2400, 2)), 0.005, 0.25, 3)


@sound("Monster", "Wingsquirrel_call", 3, 0.45)
def _(rng):
    out = silence(1.0)
    count = rng.randint(7, 13)
    rate = rng.uniform(13, 17)
    for i in range(count):
        f = rng.uniform(2600, 3400)
        out = mix(out, (chirp(rng, 0.035, f, f * 1.15, "triangle"), i / rate, rng.uniform(0.5, 1.0)))
    return out


@sound("Monster", "Wingsquirrel_roar", 2, 0.6)
def _(rng):
    voice = tone(0.4, lambda p: 2000 + 1200 * math.sin(math.pi * p), "saw")
    return env(normalize(bandpass(voice, 3000, 2)), 0.005, 0.3, 3)


@sound("Monster", "Roseviper_call", 2, 0.45)
def _(rng):
    rattle = bandpass(noise(0.8, rng), 4200, 1.5)
    rattle = [x * (1.0 if math.sin(TAU * 26 * i / SR) > 0 else 0.15) for i, x in enumerate(rattle)]
    hiss = highpass(noise(0.8, rng), 3500)
    sig = mix((normalize(rattle), 0, 0.8), (normalize(hiss), 0, 0.25))
    return shape(sig, [(0, 0), (0.1, 1), (0.7, 0.8), (1, 0)])


@sound("Boss", "SunBear_call", 2, 0.75)
def _(rng):
    return growl(rng, rng.uniform(0.9, 1.4), rng.uniform(52, 66), 20, 450)


# 제작·설치물 ----------------------------------------------------------

@sound("Craft", "craft", 1, 0.65)
def _(rng):
    taps = mix((knock(rng, 700, 0.1, 10), 0, 0.6), (knock(rng, 760, 0.1, 10), 0.12, 0.6))
    sparkle = mix(*[(chime(f, 0.5), 0.25 + i * 0.05, 0.4) for i, f in enumerate([1046.5, 1318.5, 1568.0])])
    return mix(taps, sparkle)


@sound("Craft", "cookAdd", 1, 0.6)
def _(rng):
    sizzle = highpass(noise(0.5, rng), 3000)
    return mix((shape(normalize(random_am(sizzle, rng, 40, 0.5)), [(0, 0), (0.1, 1), (1, 0)]), 0.03, 0.6), (thump(rng, 160, 0.06), 0, 0.6))


@sound("Craft", "cookDone", 1, 0.55)
def _(rng):
    return mix((chime(880, 0.7), 0, 0.7), (chime(1318.5, 0.7), 0.12, 0.6))


@sound("Craft", "smeltLoop", 1, 0.55, loop=True)
def _(rng):
    roar = random_am(lowpass(noise(3.5, rng), 280), rng, 2, 0.4)
    hum = tremolo(lowpass(tone(3.5, 55, "saw"), 200), 1.5, 0.3)
    crack = bandpass(crackle(3.5, rng, 12, 0.002), 2000, 0.7)
    return mix((normalize(roar), 0, 0.8), (normalize(hum), 0, 0.3), (normalize(crack), 0, 0.3))


@sound("Craft", "smeltDone", 1, 0.6)
def _(rng):
    return mix((chime(660, 0.8, ((1, 1), (2.4, 0.5), (3.9, 0.2))), 0, 0.8), (chime(990, 0.6), 0.1, 0.4))


@sound("Craft", "chestOpen", 1, 0.6)
def _(rng):
    creak = bandpass(tone(0.35, lambda p: 180 + 160 * p + 20 * math.sin(TAU * 12 * p), "saw"), 1100, 3)
    return mix((shape(normalize(creak), [(0, 0), (0.2, 0.8), (0.9, 0.6), (1, 0)]), 0, 0.5), (knock(rng, 250, 0.12, 4), 0.32, 0.6))


@sound("Craft", "chestClose", 1, 0.65)
def _(rng):
    return mix((knock(rng, 220, 0.16, 4), 0, 0.8), (thump(rng, 90, 0.12), 0, 0.7))


# UI ----------------------------------------------------------

@sound("UI", "uiClick", 1, 0.45)
def _(rng):
    return env(tone(0.04, sweep(1300, 900)), 0.001, 0.02)


@sound("UI", "uiHover", 1, 0.2)
def _(rng):
    return env(tone(0.03, 1900), 0.001, 0.012)


@sound("UI", "uiDeny", 1, 0.4)
def _(rng):
    buzz = lowpass(tone(0.09, 150, "square"), 1500)
    return mix((env(buzz, 0.002, 0.06), 0, 1), (env(buzz, 0.002, 0.06), 0.11, 1))


@sound("UI", "uiOpen", 1, 0.4)
def _(rng):
    return mix((whoosh(rng, 0.14, 800, 2400, 2800, 1.5), 0, 0.6), (env(tone(0.06, sweep(500, 800)), 0.002, 0.04), 0.03, 0.4))


@sound("UI", "uiClose", 1, 0.4)
def _(rng):
    return mix((whoosh(rng, 0.14, 2600, 1600, 600, 1.5), 0, 0.6), (env(tone(0.06, sweep(700, 450)), 0.002, 0.04), 0.03, 0.4))


@sound("UI", "slotPick", 1, 0.4)
def _(rng):
    return env(tone(0.05, sweep(500, 800)), 0.001, 0.03)


@sound("UI", "slotDrop", 1, 0.45)
def _(rng):
    return mix(env(tone(0.06, sweep(450, 300)), 0.001, 0.035), (env(lowpass(noise(0.03, rng), 1500), 0.001, 0.015), 0, 0.4))


@sound("UI", "mapOpen", 1, 0.45)
def _(rng):
    return rustle(rng, 0.35, 2600, 300)


@sound("UI", "pageFlip", 2, 0.4)
def _(rng):
    return whoosh(rng, 0.18, 1500, 4000, 2000, 0.8)


@sound("UI", "saveDone", 1, 0.3)
def _(rng):
    return mix((chime(1046.5, 0.4), 0, 0.6), (chime(1568.0, 0.4), 0.08, 0.5))


@sound("UI", "worldCreate", 1, 0.6)
def _(rng):
    rise = whoosh(rng, 0.8, 300, 2500, 3500, 0.7)
    chord = mix(*[(chime(f, 1.0), 0.6, 0.4) for f in [523.25, 659.25, 783.99, 1046.5]])
    return mix((rise, 0, 0.4), chord)


@sound("UI", "menuPoke", 2, 0.5)
def _(rng):
    return env(tone(0.25, lambda p: 320 * (1 + 0.15 * math.sin(TAU * 18 * p) * (1 - p))), 0.003, 0.15)


@sound("UI", "menuBreak", 1, 0.6)
def _(rng):
    pop = env(tone(0.06, sweep(900, 300)), 0.001, 0.04)
    return mix(pop, (rustle(rng, 0.35, 2400, 900), 0.02, 0.6), (thump(rng, 100, 0.12), 0, 0.5))


# 월드 ----------------------------------------------------------

@sound("World", "dawn", 1, 0.45)
def _(rng):
    notes = [392.0, 493.88, 587.33, 783.99]
    return mix(*[(chime(f, 1.4, decay=1.0), i * 0.18, 0.5) for i, f in enumerate(notes)])


@sound("World", "dusk", 1, 0.45)
def _(rng):
    notes = [659.25, 523.25, 440.0, 329.63]
    return mix(*[(chime(f, 1.4, decay=1.0), i * 0.22, 0.5) for i, f in enumerate(notes)])


@sound("World", "thunder", 2, 0.85)
def _(rng):
    crack = env(highpass(noise(0.3, rng), 1500), 0.002, 0.15)
    rumble = random_am(lowpass(noise(4.0, rng), 160), rng, 4, 0.7)
    rumble = shape(normalize(rumble), [(0, 0), (0.05, 1), (0.4, 0.7), (1, 0)])
    return mix((normalize(crack), 0, 0.5), (rumble, 0.05, 1))


def birds(rng, sec, count):
    out = silence(sec)
    for _ in range(count):
        start = rng.uniform(0, sec - 0.5)
        base = rng.uniform(2200, 3800)
        for k in range(rng.randint(2, 4)):
            chirp = env(tone(0.07, sweep(base, base * rng.uniform(1.2, 1.5))), 0.005, 0.05)
            out = mix(out, (chirp, start + k * rng.uniform(0.09, 0.14), rng.uniform(0.15, 0.3)))
    return out


@sound("World", "ambientDay", 1, 0.5, loop=True)
def _(rng):
    sec = 12.5
    wind = random_am(lowpass(noise(sec, rng), 350), rng, 0.4, 0.7)
    leaves = random_am(bandpass(noise(sec, rng), 3000, 0.8), rng, 0.5, 0.9)
    return mix((normalize(wind), 0, 0.6), (normalize(leaves), 0, 0.08), birds(rng, sec, 7))


@sound("World", "ambientNight", 1, 0.45, loop=True)
def _(rng):
    sec = 12.5
    wind = random_am(lowpass(noise(sec, rng), 250), rng, 0.3, 0.6)
    crickets = silence(sec)
    for _ in range(3):
        f = rng.uniform(4200, 5200)
        rate = rng.uniform(14, 20)
        c = tone(sec, f)
        c = [x * (1.0 if math.sin(TAU * rate * i / SR) > 0.3 else 0.0) for i, x in enumerate(c)]
        c = tremolo(c, rng.uniform(0.4, 0.8), 0.9, rng.random())
        crickets = mix(crickets, (c, 0, rng.uniform(0.04, 0.08)))
    return mix((normalize(wind), 0, 0.4), (lowpass(crickets, 6000), 0, 1))


@sound("World", "campfireLoop", 1, 0.5, loop=True)
def _(rng):
    return fire_loop(rng, 4.5, 300, 22, 0.6)


@sound("World", "lavaLoop", 1, 0.5, loop=True)
def _(rng):
    sec = 4.5
    base = random_am(lowpass(noise(sec, rng), 180), rng, 1, 0.5)
    pops = silence(sec)
    for _ in range(9):
        b = env(tone(rng.uniform(0.06, 0.12), sweep(rng.uniform(80, 120), rng.uniform(250, 400))), 0.005, 0.06)
        pops = mix(pops, (b, rng.uniform(0, sec - 0.2), rng.uniform(0.3, 0.6)))
    return mix((normalize(base), 0, 0.7), pops)


# ---------------------------------------------------------------- 출력

def write_wav(path, sig):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        frames = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, x)) * 32767)) for x in sig)
        w.writeframes(frames)


def main():
    for name, (category, fn, variants, peak, loop) in SOUNDS.items():
        for v in range(variants):
            rng = random.Random(f"{name}-{v}")
            sig = fn(rng)
            if loop:
                sig = make_loop(sig, 0.5)
                sig = normalize(sig, peak)
            else:
                sig = fade_edges(normalize(sig, peak))
            suffix = f"_{v + 1}" if variants > 1 else ""
            path = os.path.join(ROOT, category, f"{name}{suffix}.wav")
            write_wav(path, sig)
            print(f"{category}/{name}{suffix}.wav  {len(sig) / SR:.2f}s")


if __name__ == "__main__":
    main()
