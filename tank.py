# -*- coding: utf-8 -*-
"""坦克大战：剧情模式、无尽模式与 Boss 系统。

本程序只使用 Python 自带的 tkinter，PyCharm 中直接运行即可。

启动流程：主菜单 -> 玩家人数 -> 剧情/无尽模式 -> 关卡播报 -> 3、2、1 倒计时。

操作：
    单人：W/A/S/D 或方向键移动，空格射击
    双人：P1 使用 W/A/S/D + 空格；P2 使用方向键 + Enter
    P：暂停/继续    R：结束后重开    M：返回菜单    Esc：退出
"""

from __future__ import annotations

import json
import math
import random
import tkinter as tk
from tkinter import simpledialog
from dataclasses import dataclass
from pathlib import Path


# =========================
# 游戏配置
# =========================
WINDOW_WIDTH = 900
WINDOW_HEIGHT = 720
HUD_HEIGHT = 126
FIELD_TOP = HUD_HEIGHT + 4
FIELD_BOTTOM = WINDOW_HEIGHT - 4
FIELD_PADDING = 4
FRAME_MS = 16

DROP_INTERVAL_SECONDS = 15
POWERUP_LIFETIME_SECONDS = 5
EFFECT_DURATION_SECONDS = 7
DROP_INTERVAL_FRAMES = max(1, round(DROP_INTERVAL_SECONDS * 1000 / FRAME_MS))
POWERUP_LIFETIME_FRAMES = max(1, round(POWERUP_LIFETIME_SECONDS * 1000 / FRAME_MS))
EFFECT_DURATION_FRAMES = max(1, round(EFFECT_DURATION_SECONDS * 1000 / FRAME_MS))

LEVEL_INTRO_SECONDS = 1.5
LEVEL_INTRO_FRAMES = max(1, round(LEVEL_INTRO_SECONDS * 1000 / FRAME_MS))
COUNTDOWN_STEP_FRAMES = max(1, round(1000 / FRAME_MS))
COUNTDOWN_TOTAL_FRAMES = COUNTDOWN_STEP_FRAMES * 3

TANK_SIZE = 36
BULLET_SIZE = 8
POWERUP_SIZE = 30
PLAYER_SPEED = 4
ENEMY_SPEED = 2
PLAYER_BULLET_SPEED = 9
ENEMY_BULLET_SPEED = 6.5
PLAYER_FIRE_COOLDOWN = 18
ENEMY_FIRE_COOLDOWN_MIN = 48
ENEMY_FIRE_COOLDOWN_MAX = 100
MAX_BULLET_BOUNCES = 3
SMALL_VIEW_RADIUS = 145
SLOW_EFFECT_MULTIPLIER = 0.5
FAST_BULLET_MULTIPLIER = 1.6
RAPID_FIRE_RATE_MULTIPLIER = 1.5
RAPID_FIRE_COOLDOWN_MULTIPLIER = 1 / RAPID_FIRE_RATE_MULTIPLIER
PLAYER_LIVES = 3
STORY_MAX_LEVEL = 3
MAX_WAVES = STORY_MAX_LEVEL  # 兼容旧代码中的名称

MINI_BOSS_INTERVAL = 10
MEGA_BOSS_INTERVAL = 100
MINI_BOSS_HEALTH_MULTIPLIER = 100
MEGA_BOSS_HEALTH_MULTIPLIER = 1000
MAX_ENDLESS_ENEMIES = 24
UPGRADE_CHOICE_COUNT = 3
MAX_COMBO = 99
COMBO_WINDOW_SECONDS = 4.0
COMBO_WINDOW_FRAMES = max(1, round(COMBO_WINDOW_SECONDS * 1000 / FRAME_MS))
COMBO_REWARD_KILLS = 8
SURVIVAL_OBJECTIVE_SECONDS = 45
SURVIVAL_OBJECTIVE_FRAMES = max(1, round(SURVIVAL_OBJECTIVE_SECONDS * 1000 / FRAME_MS))
BLIND_BOX_OBJECTIVE_TARGET = 3
SAVE_FILE_NAME = "tank_save.json"

# 颜色
HUD_BACKGROUND = "#101a2a"
FIELD_BACKGROUND = "#17232b"
FIELD_GRID = "#1d3038"
FIELD_BORDER = "#6f8991"
TEXT_COLOR = "#f4f7f8"
MUTED_TEXT = "#a9bdc2"
PLAYER_COLOR = "#42d392"
PLAYER_TWO_COLOR = "#5aa9ff"
ENEMY_COLOR = "#ed6868"
PLAYER_BULLET_COLOR = "#ffe082"
ENEMY_BULLET_COLOR = "#ff9f68"
BRICK_FILL = "#b7623d"
BRICK_DARK = "#6f3328"
STEEL_FILL = "#607986"
STEEL_DARK = "#2c454f"
BASE_COLOR = "#5ac8fa"
BASE_DARK = "#205d8a"
EXPLOSION_YELLOW = "#ffd166"
EXPLOSION_ORANGE = "#f97316"
POWERUP_GLOW_COLOR = "#fff2a6"
FOG_COLOR = "#071018"
MENU_PANEL = "#132b38"
MENU_PANEL_LIGHT = "#1c4350"
MENU_BUTTON = "#225467"
MENU_BUTTON_HOVER = "#34798c"
MENU_ACCENT = "#62c6c2"
MINI_BOSS_COLOR = "#9b5de5"
MINI_BOSS_CORE = "#4cc9f0"
MEGA_BOSS_COLOR = "#ef476f"
MEGA_BOSS_CORE = "#ff9f1c"
BOSS_HEALTH_BACKGROUND = "#291b2f"
BOSS_HEALTH_FILL = "#ff4d6d"
LASER_WARNING_COLOR = "#ff8fa3"
LASER_ACTIVE_COLOR = "#ff1744"
SHOCKWAVE_COLOR = "#c77dff"

DIRECTIONS = {
    "up": (0.0, -1.0),
    "down": (0.0, 1.0),
    "left": (-1.0, 0.0),
    "right": (1.0, 0.0),
}

OPPOSITE_DIRECTIONS = {
    "up": "down",
    "down": "up",
    "left": "right",
    "right": "left",
}

KEY_TO_DIRECTION = {
    "up": "up",
    "down": "down",
    "left": "left",
    "right": "right",
    "w": "up",
    "s": "down",
    "a": "left",
    "d": "right",
}

BLIND_BOX_KIND = "blind_box"

# 场地只会生成神秘盲盒，具体效果在拾取时才随机决定。
POWERUP_TYPES = (BLIND_BOX_KIND,)

# 盲盒可随机分配的实际 Buff/Debuff 效果池。
BLIND_BOX_EFFECT_TYPES = (
    "bullet_speed_up",
    "rapid_fire",
    "bounce_bullets",
    "piercing_bullets",
    "slow_tank",
    "slow_bullets",
    "reverse_controls",
    "small_view",
)

POWERUP_INFO = {
    BLIND_BOX_KIND: {
        "name": "神秘盲盒",
        "short": "盲盒",
        "category": "mystery",
        "color": "#8e7dff",
        "description": "拾取后随机获得一个效果",
    },
    "bullet_speed_up": {
        "name": "子弹加速",
        "short": "弹速+",
        "category": "buff",
        "color": "#ffd166",
        "description": "子弹飞行速度提高",
    },
    "rapid_fire": {
        "name": "射击加速",
        "short": "射速+",
        "category": "buff",
        "color": "#ff8fab",
        "description": "射击速度提高 50%",
    },
    "bounce_bullets": {
        "name": "反弹炮弹",
        "short": "反弹",
        "category": "buff",
        "color": "#59d9ff",
        "description": "子弹撞墙后反弹 3 次",
    },
    "piercing_bullets": {
        "name": "穿透炮弹",
        "short": "穿透",
        "category": "buff",
        "color": "#c084fc",
        "description": "子弹穿透砖墙和钢墙",
    },
    "slow_tank": {
        "name": "坦克减速",
        "short": "减速",
        "category": "debuff",
        "color": "#ff8a65",
        "description": "移动速度降低",
    },
    "slow_bullets": {
        "name": "子弹减速",
        "short": "弹速-",
        "category": "debuff",
        "color": "#ffb86b",
        "description": "子弹飞行速度降低",
    },
    "reverse_controls": {
        "name": "操作反向",
        "short": "反向",
        "category": "debuff",
        "color": "#ff6b9d",
        "description": "移动方向全部反转",
    },
    "small_view": {
        "name": "视野缩小",
        "short": "迷雾",
        "category": "debuff",
        "color": "#b084ff",
        "description": "只能看见坦克附近区域",
    },
}


@dataclass(frozen=True)
class UpgradeDefinition:
    key: str
    name: str
    short: str
    category: str
    description: str
    max_level: int
    rarity: str = "common"


UPGRADE_DEFINITIONS = {
    definition.key: definition
    for definition in (
        UpgradeDefinition("quick_reload", "快速装填", "装填", "火力", "永久提高 12% 射击速度", 5),
        UpgradeDefinition("high_velocity", "高速炮管", "弹速", "火力", "永久提高 15% 炮弹速度", 4),
        UpgradeDefinition("heavy_shell", "重型炮弹", "重弹", "火力", "炮弹伤害 +1，但射速降低 10%", 3, "rare"),
        UpgradeDefinition("twin_shot", "双联炮", "双发", "火力", "一次发射两枚小角度散射弹", 1, "rare"),
        UpgradeDefinition("ricochet_matrix", "反弹矩阵", "反弹", "弹道", "永久增加 1 次炮弹反弹", 5),
        UpgradeDefinition("explosive_warhead", "爆裂弹头", "爆裂", "弹道", "击毁敌人时对附近敌人造成 1 点伤害", 1, "rare"),
        UpgradeDefinition("energy_shield", "能量护盾", "护盾", "生存", "每关抵挡一次致命伤害", 3, "rare"),
        UpgradeDefinition("emergency_repair", "应急维修", "维修", "生存", "击败 Boss 后恢复生命", 2, "rare"),
        UpgradeDefinition("mobility_tracks", "机动履带", "机动", "生存", "永久提高 10% 移动速度", 4),
        UpgradeDefinition("lucky_sensor", "幸运感应", "幸运", "盲盒", "提高盲盒抽中 Buff 的权重", 3),
        UpgradeDefinition("effect_extension", "效果延长", "延时", "盲盒", "玩家盲盒效果持续时间增加 25%", 3),
        UpgradeDefinition("risk_investment", "风险投资", "投资", "盲盒", "Debuff 结束时额外掉落一个盲盒", 1, "rare"),
    )
}


@dataclass(frozen=True)
class EnemyProfile:
    key: str
    name: str
    color: str
    health_multiplier: float = 1.0
    speed_multiplier: float = 1.0
    fire_multiplier: float = 1.0
    score: int = 100


ENEMY_PROFILES = {
    profile.key: profile
    for profile in (
        EnemyProfile("standard", "敌军", ENEMY_COLOR),
        EnemyProfile("scout", "侦察坦克", "#ff9f68", 0.75, 1.35, 0.90, 120),
        EnemyProfile("heavy", "重甲坦克", "#8d99ae", 2.20, 0.65, 1.20, 220),
        EnemyProfile("sniper", "狙击坦克", "#ef476f", 1.05, 0.72, 1.45, 190),
        EnemyProfile("demolisher", "爆破坦克", "#f77f00", 1.35, 0.82, 1.55, 210),
        EnemyProfile("guard", "护卫坦克", "#b565d9", 1.40, 0.90, 1.05, 160),
    )
}

ELITE_AFFIX_INFO = {
    "frenzy": ("狂热", "#ff595e"),
    "shielded": ("护盾", "#80ffdb"),
    "split_shot": ("分裂", "#ffd166"),
    "swift": ("迅捷", "#4cc9f0"),
}

OBJECTIVE_INFO = {
    "elimination": ("清剿战", "消灭全部敌人"),
    "survival": ("生存战", f"坚持 {SURVIVAL_OBJECTIVE_SECONDS} 秒"),
    "ace_hunt": ("王牌猎杀", "击败精英指挥官"),
    "blind_box_race": ("盲盒争夺", f"玩家阵营开启 {BLIND_BOX_OBJECTIVE_TARGET} 个盲盒"),
    "boss": ("Boss 战", "击败当前 Boss"),
}

CHALLENGE_INFO = {
    "enemy_horde": ("敌潮压境", "敌人数量 +30%", 1.25),
    "elite_invasion": ("精英入侵", "精英出现率翻倍", 1.30),
    "chaotic_boxes": ("混沌盲盒", "Buff 与 Debuff 权重固定相同", 1.20),
    "base_alarm": ("基地警报", "基地可承受两次攻击", 1.15),
    "one_life": ("孤注一掷", "玩家只有 1 条生命", 1.60),
}

BOSS_MUTATION_INFO = {
    "berserk_core": ("狂暴核心", "攻击与技能冷却更短"),
    "reflective_shell": ("反射外壳", "周期性获得减伤护盾"),
    "box_devourer": ("盲盒吞噬者", "会吞噬盲盒并恢复生命"),
}


# =========================
# 辅助函数
# =========================
def rectangles_overlap(
    first: tuple[float, float, float, float],
    second: tuple[float, float, float, float],
) -> bool:
    """判断两个矩形是否相交。"""
    return (
        first[0] < second[0] + second[2]
        and first[0] + first[2] > second[0]
        and first[1] < second[1] + second[3]
        and first[1] + first[3] > second[1]
    )


def center_of(rectangle: tuple[float, float, float, float]) -> tuple[float, float]:
    return rectangle[0] + rectangle[2] / 2, rectangle[1] + rectangle[3] / 2


def normalized_vector(dx: float, dy: float) -> tuple[float, float]:
    length = math.hypot(dx, dy)
    if length <= 0.0001:
        return 0.0, -1.0
    return dx / length, dy / length


def vector_to_direction(vx: float, vy: float) -> str:
    if abs(vx) > abs(vy):
        return "right" if vx > 0 else "left"
    return "down" if vy > 0 else "up"


def rapid_fire_cooldown(base_cooldown: int | float) -> int:
    """将射击冷却换算为 1.5 倍射击速度。"""
    return max(1, math.ceil(float(base_cooldown) * RAPID_FIRE_COOLDOWN_MULTIPLIER))


def regular_enemy_health(level: int) -> int:
    """当前关卡普通敌人的基础生命值。"""
    return max(1, 1 + (max(1, level) - 1) // 3)


def regular_enemy_count(level: int) -> int:
    """无尽模式普通敌人数量，逐关增加并设置上限。"""
    level = max(1, level)
    return min(MAX_ENDLESS_ENEMIES, 3 + (level - 1) // 2 + min(8, level // 10))


def enemy_speed_for_level(level: int) -> float:
    """敌人速度随关卡严格增加，高关卡继续获得少量对数增幅。"""
    growth = max(0, int(level) - 1)
    linear_growth = min(4.5, growth * 0.035)
    endless_growth = math.log1p(growth) * 0.03
    return ENEMY_SPEED + linear_growth + endless_growth


def enemy_fire_cooldown_for_level(level: int) -> tuple[int, int]:
    """敌人射击冷却随关卡提高而缩短。"""
    reduction = min(58, max(0, level - 1) * 2)
    return max(24, ENEMY_FIRE_COOLDOWN_MIN - reduction // 2), max(
        40,
        ENEMY_FIRE_COOLDOWN_MAX - reduction,
    )


@dataclass
class Wall:
    x: float
    y: float
    width: float
    height: float
    kind: str = "brick"
    hp: int = 1
    alive: bool = True
    unbreakable: bool = False

    @property
    def rect(self) -> tuple[float, float, float, float]:
        return self.x, self.y, self.width, self.height

    def draw(self, canvas: tk.Canvas) -> None:
        if not self.alive:
            return
        if self.kind == "brick":
            canvas.create_rectangle(
                self.x,
                self.y,
                self.x + self.width,
                self.y + self.height,
                fill=BRICK_FILL,
                outline=BRICK_DARK,
                width=2,
            )
            middle_x = self.x + self.width / 2
            middle_y = self.y + self.height / 2
            canvas.create_line(
                middle_x,
                self.y + 2,
                middle_x,
                self.y + self.height - 2,
                fill="#d88955",
            )
            canvas.create_line(
                self.x + 2,
                middle_y,
                self.x + self.width - 2,
                middle_y,
                fill="#8c432f",
            )
        else:
            canvas.create_rectangle(
                self.x,
                self.y,
                self.x + self.width,
                self.y + self.height,
                fill=STEEL_FILL,
                outline=STEEL_DARK,
                width=2,
            )
            for bolt_x, bolt_y in (
                (self.x + 6, self.y + 6),
                (self.x + self.width - 6, self.y + 6),
                (self.x + 6, self.y + self.height - 6),
                (self.x + self.width - 6, self.y + self.height - 6),
            ):
                canvas.create_oval(
                    bolt_x - 2,
                    bolt_y - 2,
                    bolt_x + 2,
                    bolt_y + 2,
                    fill="#b7d0d4",
                    outline="",
                )


class Base:
    def __init__(self, x: float, y: float, width: float = 48, height: float = 42) -> None:
        self.x = x
        self.y = y
        self.width = width
        self.height = height
        self.alive = True

    @property
    def rect(self) -> tuple[float, float, float, float]:
        return self.x, self.y, self.width, self.height

    def draw(self, canvas: tk.Canvas) -> None:
        if self.alive:
            canvas.create_rectangle(
                self.x,
                self.y + 10,
                self.x + self.width,
                self.y + self.height,
                fill=BASE_DARK,
                outline="#0b2a42",
                width=2,
            )
            canvas.create_polygon(
                self.x + self.width / 2,
                self.y,
                self.x + self.width - 5,
                self.y + 12,
                self.x + 5,
                self.y + 12,
                fill=BASE_COLOR,
                outline="#d6f3ff",
            )
            canvas.create_rectangle(
                self.x + 18,
                self.y + 17,
                self.x + self.width - 18,
                self.y + self.height - 5,
                fill="#163f5a",
                outline="#8ee2ff",
            )
            canvas.create_text(
                self.x + self.width / 2,
                self.y + self.height - 13,
                text="基地",
                fill="#d6f3ff",
                font=("Microsoft YaHei", 8, "bold"),
            )
        else:
            canvas.create_rectangle(
                self.x,
                self.y + 14,
                self.x + self.width,
                self.y + self.height,
                fill="#38434a",
                outline="#1d2529",
                width=2,
            )
            canvas.create_line(
                self.x + 4,
                self.y + 8,
                self.x + self.width - 4,
                self.y + self.height - 2,
                fill="#ff755f",
                width=3,
            )
            canvas.create_line(
                self.x + self.width - 4,
                self.y + 8,
                self.x + 4,
                self.y + self.height - 2,
                fill="#ff755f",
                width=3,
            )


class PowerUp:
    """场地上的临时盲盒，出现五秒后自动消失。

    直接传入具体效果类型仍保留兼容性；正常掉落路径只会传入
    ``BLIND_BOX_KIND``，具体效果在拾取时才随机分配。
    """

    def __init__(
        self,
        kind: str,
        x: float,
        y: float,
        assigned_effect: str | None = None,
    ) -> None:
        if kind not in POWERUP_INFO:
            raise ValueError(f"未知道具类型：{kind}")
        if assigned_effect is not None and assigned_effect not in BLIND_BOX_EFFECT_TYPES:
            raise ValueError(f"未知盲盒效果：{assigned_effect}")
        self.kind = kind
        self.assigned_effect = assigned_effect
        self.x = float(x)
        self.y = float(y)
        self.width = POWERUP_SIZE
        self.height = POWERUP_SIZE
        self.remaining_frames = POWERUP_LIFETIME_FRAMES

    @property
    def rect(self) -> tuple[float, float, float, float]:
        return self.x, self.y, self.width, self.height

    @property
    def center(self) -> tuple[float, float]:
        return center_of(self.rect)

    def draw(self, canvas: tk.Canvas, frame: int = 0) -> None:
        # 所有场地道具统一以盲盒外观展示，避免在拾取前泄露具体效果。
        info = POWERUP_INFO[BLIND_BOX_KIND]
        center_x, center_y = self.center
        pulse = 2.0 + math.sin(frame / 8.0) * 2.0
        half_size = self.width / 2 + pulse
        glow_half = half_size + 5
        canvas.create_oval(
            center_x - glow_half,
            center_y - glow_half,
            center_x + glow_half,
            center_y + glow_half,
            outline=POWERUP_GLOW_COLOR,
            width=2,
        )
        canvas.create_polygon(
            center_x,
            center_y - half_size,
            center_x + half_size,
            center_y,
            center_x,
            center_y + half_size,
            center_x - half_size,
            center_y,
            fill=info["color"],
            outline="#18242a",
            width=2,
        )
        canvas.create_oval(
            center_x - 7,
            center_y - 7,
            center_x + 7,
            center_y + 7,
            fill="#18242a",
            outline="#f8fbdd",
        )
        canvas.create_text(
            center_x,
            center_y,
            text=info["short"],
            fill="#ffffff",
            font=("Microsoft YaHei", 7, "bold"),
        )
        canvas.create_text(
            center_x,
            self.y + self.height + 10,
            text=f"{info['name']} {max(1, math.ceil(self.remaining_frames * FRAME_MS / 1000))}s",
            fill=TEXT_COLOR,
            font=("Microsoft YaHei", 8, "bold"),
        )


class Bullet:
    """支持普通、反弹、穿透和追踪属性的炮弹。"""

    def __init__(
        self,
        owner: Tank,
        team: str,
        x: float,
        y: float,
        direction: str = "up",
        speed: float = PLAYER_BULLET_SPEED,
        bounce_remaining: int = 0,
        piercing: bool = False,
        damage: int = 1,
        homing: bool = False,
        target: Tank | None = None,
        visual_kind: str = "normal",
        vector: tuple[float, float] | None = None,
        explosive: bool = False,
    ) -> None:
        self.owner = owner
        self.team = team
        self.x = float(x)
        self.y = float(y)
        self.previous_x = self.x
        self.previous_y = self.y
        self.speed = max(1.0, float(speed))
        self.bounce_remaining = max(0, int(bounce_remaining))
        self.piercing = piercing
        self.damage = max(1, int(damage))
        self.homing = homing
        self.target = target
        self.visual_kind = visual_kind
        self.explosive = explosive
        self.active = True
        self.hit_targets: set[int] = set()
        self.hit_walls: set[int] = set()
        if vector is None:
            self.direction = direction if direction in DIRECTIONS else "up"
            self.vx, self.vy = DIRECTIONS[self.direction]
        else:
            self.vx, self.vy = normalized_vector(vector[0], vector[1])
            self.direction = vector_to_direction(self.vx, self.vy)

    @property
    def rect(self) -> tuple[float, float, float, float]:
        return self.x, self.y, BULLET_SIZE, BULLET_SIZE

    @property
    def center(self) -> tuple[float, float]:
        return self.x + BULLET_SIZE / 2, self.y + BULLET_SIZE / 2

    def update(self) -> None:
        self.previous_x = self.x
        self.previous_y = self.y
        if self.homing and self.target is not None and self.target.alive:
            target_x, target_y = self.target.center
            current_x, current_y = self.center
            wanted_x, wanted_y = normalized_vector(target_x - current_x, target_y - current_y)
            self.vx, self.vy = normalized_vector(
                self.vx * 0.90 + wanted_x * 0.10,
                self.vy * 0.90 + wanted_y * 0.10,
            )
            self.direction = vector_to_direction(self.vx, self.vy)
        self.x += self.vx * self.speed
        self.y += self.vy * self.speed

    def reflect_from_wall(self, wall: Wall) -> None:
        movement_x = self.x - self.previous_x
        movement_y = self.y - self.previous_y
        if abs(movement_x) > abs(movement_y):
            self.vx = -self.vx
            if movement_x > 0:
                self.x = wall.x + wall.width + BULLET_SIZE / 2 + 2
            else:
                self.x = wall.x - BULLET_SIZE / 2 - 2
        else:
            self.vy = -self.vy
            if movement_y > 0:
                self.y = wall.y + wall.height + BULLET_SIZE / 2 + 2
            else:
                self.y = wall.y - BULLET_SIZE / 2 - 2
        self.vx, self.vy = normalized_vector(self.vx, self.vy)
        self.direction = vector_to_direction(self.vx, self.vy)

    def draw(self, canvas: tk.Canvas) -> None:
        if self.explosive:
            color = "#ff7b00"
            outline = "#fff3b0"
        elif self.visual_kind == "boss":
            color = "#ff6b6b"
            outline = "#ffd166"
        elif self.visual_kind == "fan":
            color = "#ff9f1c"
            outline = "#fff0a8"
        else:
            color = PLAYER_BULLET_COLOR if self.team == "player" else ENEMY_BULLET_COLOR
            if self.piercing:
                outline = "#d9a7ff"
            elif self.bounce_remaining > 0:
                outline = "#8be9fd"
            else:
                outline = "#4a2c1c"
        center_x, center_y = self.center
        canvas.create_oval(
            self.x - 2,
            self.y - 2,
            self.x + BULLET_SIZE + 2,
            self.y + BULLET_SIZE + 2,
            fill="#fff4c2",
            outline="",
        )
        canvas.create_oval(
            self.x,
            self.y,
            self.x + BULLET_SIZE,
            self.y + BULLET_SIZE,
            fill=color,
            outline=outline,
            width=2,
        )
        canvas.create_oval(
            center_x - 1.5,
            center_y - 1.5,
            center_x + 1.5,
            center_y + 1.5,
            fill="#ffffff",
            outline="",
        )


class Tank:
    """玩家与普通敌人的公共基类。"""

    def __init__(
        self,
        x: float,
        y: float,
        color: str,
        team: str,
        speed: float,
        label: str = "",
        max_health: int = 1,
    ) -> None:
        self.x = float(x)
        self.y = float(y)
        self.width = TANK_SIZE
        self.height = TANK_SIZE
        self.color = color
        self.team = team
        self.base_speed = float(speed)
        self.speed = float(speed)
        self.direction = "up"
        self.fire_cooldown = 0
        self.spawn_protection = 0
        self.alive = True
        self.hit_flash = 0
        self.label = label or ("敌军" if team == "enemy" else "玩家")
        self.max_health = max(1, int(max_health))
        self.health = self.max_health
        self.effects: dict[str, int] = {}
        self.fire_rate_multiplier = 1.0
        self.bullet_speed_multiplier = 1.0
        self.move_speed_multiplier = 1.0
        self.bullet_damage = 1
        self.permanent_bounces = 0
        self.twin_shot = False
        self.explosive_shots = False
        self.shield_per_level = 0
        self.shield_charges = 0
        self.repair_on_boss = 0
        self.blind_box_luck = 0.0
        self.effect_duration_multiplier = 1.0
        self.risk_investment = False
        self.is_boss = False

    @property
    def rect(self) -> tuple[float, float, float, float]:
        return self.x, self.y, self.width, self.height

    @property
    def center(self) -> tuple[float, float]:
        return center_of(self.rect)

    def update_timers(self) -> None:
        if self.fire_cooldown > 0:
            self.fire_cooldown -= 1
        if self.spawn_protection > 0:
            self.spawn_protection -= 1
        if self.hit_flash > 0:
            self.hit_flash -= 1

    def fire(
        self,
        bullet_speed: float | None = None,
        bounce_remaining: int = 0,
        piercing: bool = False,
        damage: int = 1,
        homing: bool = False,
        target: Tank | None = None,
        visual_kind: str = "normal",
        explosive: bool = False,
    ) -> Bullet | None:
        if not self.alive or (self.team == "player" and self.fire_cooldown > 0):
            return None

        center_x, center_y = self.center
        if self.direction == "up":
            bullet_x = center_x - BULLET_SIZE / 2
            bullet_y = self.y - BULLET_SIZE - 4
        elif self.direction == "down":
            bullet_x = center_x - BULLET_SIZE / 2
            bullet_y = self.y + self.height + 4
        elif self.direction == "left":
            bullet_x = self.x - BULLET_SIZE - 4
            bullet_y = center_y - BULLET_SIZE / 2
        else:
            bullet_x = self.x + self.width + 4
            bullet_y = center_y - BULLET_SIZE / 2

        # 敌方有独立的 fire_timer / normal_fire_timer 控制节奏，
        # 这里不再叠加 Tank.fire_cooldown，避免双重冷却。
        if self.team == "player":
            permanent_cooldown = max(1, math.ceil(PLAYER_FIRE_COOLDOWN / max(0.25, self.fire_rate_multiplier)))
            if self.effects.get("rapid_fire", 0) > 0:
                self.fire_cooldown = rapid_fire_cooldown(permanent_cooldown)
            else:
                self.fire_cooldown = permanent_cooldown

        if self.team == "player":
            if bullet_speed is None:
                bullet_speed = PLAYER_BULLET_SPEED
        else:
            if bullet_speed is None:
                bullet_speed = ENEMY_BULLET_SPEED

        return Bullet(
            owner=self,
            team="player" if self.team == "player" else "enemy",
            x=bullet_x,
            y=bullet_y,
            direction=self.direction,
            speed=bullet_speed,
            bounce_remaining=bounce_remaining,
            piercing=piercing,
            damage=damage,
            homing=homing,
            target=target,
            visual_kind=visual_kind,
            explosive=explosive,
        )

    def take_damage(self, amount: int = 1) -> bool:
        if not self.alive:
            return False
        self.health -= max(1, int(amount))
        self.hit_flash = 8
        if self.health <= 0:
            self.health = 0
            self.alive = False
            return True
        return False

    def draw_health_bar(self, canvas: tk.Canvas, width: float | None = None, y_offset: float = -9) -> None:
        if not self.alive or self.max_health <= 1:
            return
        bar_width = width or self.width
        left = self.x + (self.width - bar_width) / 2
        top = self.y + y_offset
        ratio = max(0.0, min(1.0, self.health / self.max_health))
        canvas.create_rectangle(
            left,
            top,
            left + bar_width,
            top + 5,
            fill="#251c25",
            outline="#101318",
        )
        canvas.create_rectangle(
            left + 1,
            top + 1,
            left + 1 + (bar_width - 2) * ratio,
            top + 4,
            fill="#70e000" if ratio > 0.35 else "#ff595e",
            outline="",
        )

    def draw(self, canvas: tk.Canvas) -> None:
        if not self.alive:
            return
        center_x, center_y = self.center
        if self.spawn_protection > 0 and (self.spawn_protection // 5) % 2 == 0:
            outline_color = "#ffffff"
        elif self.hit_flash > 0:
            outline_color = "#fff3a3"
        else:
            outline_color = "#0c171a"

        canvas.create_oval(
            self.x + 3,
            self.y + self.height - 1,
            self.x + self.width + 3,
            self.y + self.height + 6,
            fill="#0b1518",
            outline="",
        )

        if self.direction in ("up", "down"):
            canvas.create_rectangle(
                self.x + 2,
                self.y + 3,
                self.x + 9,
                self.y + self.height - 3,
                fill="#24343a",
                outline="#0b171b",
            )
            canvas.create_rectangle(
                self.x + self.width - 9,
                self.y + 3,
                self.x + self.width - 2,
                self.y + self.height - 3,
                fill="#24343a",
                outline="#0b171b",
            )
            for track_y in (self.y + 9, self.y + 18, self.y + 27):
                canvas.create_line(self.x + 3, track_y, self.x + 8, track_y, fill="#61777c")
                canvas.create_line(
                    self.x + self.width - 8,
                    track_y,
                    self.x + self.width - 3,
                    track_y,
                    fill="#61777c",
                )
        else:
            canvas.create_rectangle(
                self.x + 3,
                self.y + 2,
                self.x + self.width - 3,
                self.y + 9,
                fill="#24343a",
                outline="#0b171b",
            )
            canvas.create_rectangle(
                self.x + 3,
                self.y + self.height - 9,
                self.x + self.width - 3,
                self.y + self.height - 2,
                fill="#24343a",
                outline="#0b171b",
            )
            for track_x in (self.x + 9, self.x + 18, self.x + 27):
                canvas.create_line(track_x, self.y + 3, track_x, self.y + 8, fill="#61777c")
                canvas.create_line(
                    track_x,
                    self.y + self.height - 8,
                    track_x,
                    self.y + self.height - 3,
                    fill="#61777c",
                )

        canvas.create_rectangle(
            self.x + 8,
            self.y + 8,
            self.x + self.width - 8,
            self.y + self.height - 8,
            fill=self.color,
            outline=outline_color,
            width=2,
        )
        if self.direction == "up":
            cannon = (center_x - 4, self.y - 11, center_x + 4, center_y + 2)
        elif self.direction == "down":
            cannon = (center_x - 4, center_y - 2, center_x + 4, self.y + self.height + 11)
        elif self.direction == "left":
            cannon = (self.x - 11, center_y - 4, center_x + 2, center_y + 4)
        else:
            cannon = (center_x - 2, center_y - 4, self.x + self.width + 11, center_y + 4)
        canvas.create_rectangle(
            *cannon,
            fill="#d9e7e8",
            outline="#152329",
            width=2,
        )
        canvas.create_oval(
            center_x - 9,
            center_y - 9,
            center_x + 9,
            center_y + 9,
            fill=self.color,
            outline=outline_color,
            width=2,
        )
        canvas.create_oval(
            center_x - 3,
            center_y - 3,
            center_x + 3,
            center_y + 3,
            fill="#eaf6f4",
            outline="#17313a",
        )
        if self.team == "player":
            canvas.create_text(
                center_x,
                self.y + self.height - 3,
                text=self.label,
                fill="#ffffff",
                font=("Arial", 7, "bold"),
            )
        self.draw_health_bar(canvas)


class PlayerTank(Tank):
    def __init__(
        self,
        player_id: int,
        x: float,
        y: float,
        color: str,
        move_keys: set[str],
        fire_keys: set[str],
    ) -> None:
        super().__init__(x, y, color, "player", PLAYER_SPEED, f"P{player_id}", max_health=1)
        self.player_id = player_id
        self.lives = PLAYER_LIVES
        self.spawn_x = float(x)
        self.spawn_y = float(y)
        self.move_keys = set(move_keys)
        self.fire_keys = set(fire_keys)
        self.respawn_timer = 0
        self.run_upgrades: dict[str, int] = {}
        self.stats = {
            "kills": 0,
            "elite_kills": 0,
            "boss_damage": 0,
            "damage_taken": 0,
            "blind_boxes": 0,
            "cover_kills": 0,
        }

    def upgrade_level(self, key: str) -> int:
        return self.run_upgrades.get(key, 0)

    def apply_upgrade(self, key: str) -> bool:
        definition = UPGRADE_DEFINITIONS.get(key)
        if definition is None or self.upgrade_level(key) >= definition.max_level:
            return False
        self.run_upgrades[key] = self.upgrade_level(key) + 1
        if key == "quick_reload":
            self.fire_rate_multiplier *= 1.12
        elif key == "high_velocity":
            self.bullet_speed_multiplier *= 1.15
        elif key == "heavy_shell":
            self.bullet_damage += 1
            self.fire_rate_multiplier *= 0.90
        elif key == "twin_shot":
            self.twin_shot = True
        elif key == "ricochet_matrix":
            self.permanent_bounces += 1
        elif key == "explosive_warhead":
            self.explosive_shots = True
        elif key == "energy_shield":
            self.shield_per_level += 1
            self.shield_charges += 1
        elif key == "emergency_repair":
            self.repair_on_boss += 1
        elif key == "mobility_tracks":
            self.move_speed_multiplier *= 1.10
        elif key == "lucky_sensor":
            self.blind_box_luck += 0.50
        elif key == "effect_extension":
            self.effect_duration_multiplier *= 1.25
        elif key == "risk_investment":
            self.risk_investment = True
        return True

    def reset_level_resources(self) -> None:
        self.shield_charges = self.shield_per_level


class EnemyTank(Tank):
    """拥有不同兵种、精英词缀且可拾取盲盒的敌人。"""

    def __init__(
        self,
        x: float,
        y: float,
        level: int = 1,
        health: int | None = None,
        guard: bool = False,
        archetype: str = "standard",
        affixes: tuple[str, ...] | list[str] | set[str] = (),
        rng: random.Random | None = None,
    ) -> None:
        rng = rng or random
        self.level = max(1, int(level))
        if guard and archetype == "standard":
            archetype = "guard"
        self.archetype = archetype if archetype in ENEMY_PROFILES else "standard"
        self.profile = ENEMY_PROFILES[self.archetype]
        self.guard = guard or self.archetype == "guard"
        self.affixes = {affix for affix in affixes if affix in ELITE_AFFIX_INFO}
        self.is_elite = bool(self.affixes)
        base_health = health if health is not None else regular_enemy_health(self.level)
        max_health = max(1, math.ceil(base_health * self.profile.health_multiplier))
        if self.is_elite:
            max_health = max(2, math.ceil(max_health * 1.8))
        speed = enemy_speed_for_level(self.level) * self.profile.speed_multiplier
        if "swift" in self.affixes:
            speed *= 1.12
        label = self.profile.name
        if self.is_elite:
            names = "/".join(ELITE_AFFIX_INFO[affix][0] for affix in sorted(self.affixes))
            label = f"精英·{names}{label}"
        super().__init__(
            x,
            y,
            self.profile.color,
            "enemy",
            speed,
            label,
            max_health=max_health,
        )
        self.score_value = self.profile.score * (2 if self.is_elite else 1)
        self.profile_fire_multiplier = self.profile.fire_multiplier
        if "frenzy" in self.affixes:
            self.profile_fire_multiplier *= 0.68
        self.direction_timer = rng.randint(20, 70)
        low, high = enemy_fire_cooldown_for_level(self.level)
        self.fire_timer = rng.randint(
            max(8, round(low * self.profile_fire_multiplier)),
            max(10, round(high * self.profile_fire_multiplier)),
        )
        self.spawn_protection = 35
        self.armor_charges = 1 if self.archetype == "heavy" else 0
        self.affix_shield_charges = 1 if "shielded" in self.affixes else 0
        self.affix_shield_timer = 300
        self.swift_timer = 0
        self.aim_timer = 0
        self.aim_target: PlayerTank | None = None

    def update_timers(self) -> None:
        super().update_timers()
        if self.swift_timer > 0:
            self.swift_timer -= 1
            if self.swift_timer == 0:
                self.move_speed_multiplier = 1.0
        if "shielded" in self.affixes and self.affix_shield_charges <= 0:
            self.affix_shield_timer -= 1
            if self.affix_shield_timer <= 0:
                self.affix_shield_charges = 1
                self.affix_shield_timer = 300
        if self.aim_timer > 0:
            self.aim_timer -= 1

    def take_damage(self, amount: int = 1) -> bool:
        if not self.alive:
            return False
        if self.affix_shield_charges > 0:
            self.affix_shield_charges -= 1
            self.affix_shield_timer = 300
            self.hit_flash = 10
            return False
        if self.armor_charges > 0:
            self.armor_charges -= 1
            amount = max(0, int(amount) - 1)
            self.hit_flash = 10
            if amount <= 0:
                return False
        if "swift" in self.affixes:
            self.swift_timer = 80
            self.move_speed_multiplier = 1.45
        return super().take_damage(amount)

    def choose_target(self, game: TankBattleGame) -> PlayerTank | None:
        alive_players = [player for player in game.players if player.alive]
        if not alive_players:
            return None
        return min(
            alive_players,
            key=lambda player: (player.x - self.x) ** 2 + (player.y - self.y) ** 2,
        )

    def choose_direction(self, game: TankBattleGame) -> str:
        if self.archetype == "scout" and game.powerups:
            item = min(
                game.powerups,
                key=lambda powerup: (powerup.x - self.x) ** 2 + (powerup.y - self.y) ** 2,
            )
            dx = item.x - self.x
            dy = item.y - self.y
        elif self.archetype == "guard":
            boss = game.current_boss()
            if boss is not None and boss is not self:
                dx = boss.x - self.x
                dy = boss.y - self.y
            else:
                target = self.choose_target(game)
                if target is None:
                    return game.run_rng.choice(tuple(DIRECTIONS))
                dx = target.x - self.x
                dy = target.y - self.y
        else:
            target = self.choose_target(game)
            if target is None or game.run_rng.random() >= 0.72:
                return game.run_rng.choice(tuple(DIRECTIONS))
            dx = target.x - self.x
            dy = target.y - self.y
        if abs(dx) > abs(dy):
            return "right" if dx > 0 else "left"
        return "down" if dy > 0 else "up"

    def is_aligned_with_target(
        self,
        game: TankBattleGame,
        target: PlayerTank | None = None,
    ) -> bool:
        target = target or self.choose_target(game)
        if target is None:
            return False
        target_x, target_y = target.center
        own_x, own_y = self.center
        return (
            abs(own_x - target_x) < TANK_SIZE * 0.65
            or abs(own_y - target_y) < TANK_SIZE * 0.65
        )

    def next_fire_delay(self, game: TankBattleGame) -> int:
        low, high = enemy_fire_cooldown_for_level(self.level)
        low = max(8, round(low * self.profile_fire_multiplier))
        high = max(low, round(high * self.profile_fire_multiplier))
        if game.effect_active("rapid_fire", self):
            low = rapid_fire_cooldown(low)
            high = rapid_fire_cooldown(high)
        return game.run_rng.randint(low, max(low, high))

    def fire_at_target(self, game: TankBattleGame, target: PlayerTank | None) -> None:
        bounce_remaining, piercing = game.tank_bullet_traits(self)
        bullet_speed = game.tank_bullet_speed(self)
        damage = 1
        explosive = self.archetype == "demolisher"
        if self.archetype == "sniper":
            bullet_speed *= 1.65
            damage = 2
        bullet = self.fire(
            bullet_speed=bullet_speed,
            bounce_remaining=bounce_remaining,
            piercing=piercing,
            damage=damage,
            target=target,
            visual_kind="fan" if "split_shot" in self.affixes else "normal",
            explosive=explosive,
        )
        if bullet is None:
            return
        if "split_shot" not in self.affixes:
            game.bullets.append(bullet)
            return
        base_angles = {
            "up": -math.pi / 2,
            "down": math.pi / 2,
            "left": math.pi,
            "right": 0.0,
        }
        base_angle = base_angles[self.direction]
        for offset in (-0.24, 0.0, 0.24):
            game.bullets.append(
                Bullet(
                    owner=self,
                    team="enemy",
                    x=bullet.x,
                    y=bullet.y,
                    speed=bullet_speed,
                    bounce_remaining=bounce_remaining,
                    piercing=piercing,
                    damage=damage,
                    visual_kind="fan",
                    vector=(math.cos(base_angle + offset), math.sin(base_angle + offset)),
                    explosive=explosive,
                )
            )

    def ai_update(self, game: TankBattleGame) -> None:
        if not self.alive:
            return
        self.direction_timer -= 1
        self.fire_timer -= 1
        target = self.choose_target(game)
        self.last_target = target

        aiming = self.archetype == "sniper" and self.aim_target is not None
        if self.direction_timer <= 0 and not aiming:
            self.direction = self.choose_direction(game)
            self.direction_timer = game.run_rng.randint(28, 78)

        if not aiming and (self.archetype != "sniper" or game.run_rng.random() < 0.28):
            movement_direction = self.direction
            if game.effect_active("reverse_controls", self):
                movement_direction = OPPOSITE_DIRECTIONS[movement_direction]
            dx, dy = DIRECTIONS[movement_direction]
            if not game.try_move_tank(self, dx, dy):
                self.direction = game.run_rng.choice(tuple(DIRECTIONS))
                self.direction_timer = game.run_rng.randint(12, 35)

        if self.archetype == "sniper":
            if self.aim_target is not None and self.aim_timer <= 0:
                target = self.aim_target if self.aim_target.alive else target
                if target is not None:
                    dx = target.center[0] - self.center[0]
                    dy = target.center[1] - self.center[1]
                    self.direction = vector_to_direction(dx, dy)
                self.fire_at_target(game, target)
                self.aim_target = None
                self.fire_timer = self.next_fire_delay(game)
            elif self.fire_timer <= 0 and self.aim_target is None and target is not None:
                self.aim_target = target
                self.aim_timer = 42
                self.fire_timer = 42
            return

        if self.fire_timer <= 0:
            if self.is_aligned_with_target(game, target) or game.run_rng.random() < 0.34:
                self.fire_at_target(game, target)
            self.fire_timer = self.next_fire_delay(game)

    def draw(self, canvas: tk.Canvas) -> None:
        super().draw(canvas)
        if not self.alive:
            return
        center_x, _ = self.center
        if self.aim_target is not None and self.aim_target.alive and self.aim_timer > 0:
            canvas.create_line(
                center_x,
                self.y + self.height / 2,
                *self.aim_target.center,
                fill="#ff6b6b",
                width=2,
                dash=(7, 5),
            )
        if self.is_elite:
            canvas.create_rectangle(
                self.x - 4,
                self.y - 4,
                self.x + self.width + 4,
                self.y + self.height + 4,
                outline="#ffd166",
                width=2,
                dash=(5, 3),
            )
        if self.affix_shield_charges > 0:
            canvas.create_oval(
                self.x - 7,
                self.y - 7,
                self.x + self.width + 7,
                self.y + self.height + 7,
                outline="#80ffdb",
                width=2,
            )
        canvas.create_text(
            center_x,
            self.y - 15,
            text=self.label,
            fill="#ffd166" if self.is_elite else MUTED_TEXT,
            font=("Microsoft YaHei", 7, "bold"),
        )


class Hazard:
    """Boss 技能危险区域基类。"""

    def __init__(self, owner: Boss | None = None) -> None:
        self.owner = owner
        self.active = True

    def update(self, game: TankBattleGame) -> bool:
        return self.active

    def draw(self, canvas: tk.Canvas) -> None:
        pass


class ShockwaveHazard(Hazard):
    def __init__(
        self,
        x: float,
        y: float,
        max_radius: float,
        speed: float,
        damage: int = 1,
        owner: Boss | None = None,
    ) -> None:
        super().__init__(owner)
        self.x = x
        self.y = y
        self.radius = 12.0
        self.max_radius = max_radius
        self.speed = speed
        self.damage = damage
        self.age = 0
        self.hit_cooldowns: dict[int, int] = {}

    def update(self, game: TankBattleGame) -> bool:
        self.age += 1
        self.radius += self.speed
        for player_id in list(self.hit_cooldowns):
            self.hit_cooldowns[player_id] -= 1
            if self.hit_cooldowns[player_id] <= 0:
                del self.hit_cooldowns[player_id]
        for player in game.players:
            if not player.alive:
                continue
            distance = math.hypot(player.center[0] - self.x, player.center[1] - self.y)
            if abs(distance - self.radius) <= 19 and id(player) not in self.hit_cooldowns:
                game.damage_player(player, self.damage, source="冲击波")
                self.hit_cooldowns[id(player)] = 34
        return self.age < 70 and self.radius < self.max_radius

    def draw(self, canvas: tk.Canvas) -> None:
        canvas.create_oval(
            self.x - self.radius,
            self.y - self.radius,
            self.x + self.radius,
            self.y + self.radius,
            outline=SHOCKWAVE_COLOR,
            width=3,
        )
        canvas.create_oval(
            self.x - max(2, self.radius - 9),
            self.y - max(2, self.radius - 9),
            self.x + max(2, self.radius - 9),
            self.y + max(2, self.radius - 9),
            outline="#f3c4ff",
            width=1,
        )


class LaserHazard(Hazard):
    def __init__(
        self,
        orientation: str,
        position: float,
        owner: Boss | None = None,
        warning_frames: int = 26,
        active_frames: int = 48,
        damage: int = 1,
    ) -> None:
        super().__init__(owner)
        self.orientation = orientation
        self.position = position
        self.warning_frames = warning_frames
        self.active_frames = active_frames
        self.damage = damage
        self.age = 0
        self.hit_cooldowns: dict[int, int] = {}

    @property
    def is_active(self) -> bool:
        return self.age >= self.warning_frames

    @property
    def rect(self) -> tuple[float, float, float, float]:
        if self.orientation == "horizontal":
            return 0, self.position - 9, WINDOW_WIDTH, 18
        return self.position - 9, FIELD_TOP, 18, FIELD_BOTTOM - FIELD_TOP

    def update(self, game: TankBattleGame) -> bool:
        self.age += 1
        for player_id in list(self.hit_cooldowns):
            self.hit_cooldowns[player_id] -= 1
            if self.hit_cooldowns[player_id] <= 0:
                del self.hit_cooldowns[player_id]
        if self.is_active:
            for player in game.players:
                if (
                    player.alive
                    and id(player) not in self.hit_cooldowns
                    and rectangles_overlap(player.rect, self.rect)
                ):
                    game.damage_player(player, self.damage, source="熔核激光")
                    self.hit_cooldowns[id(player)] = 22
        return self.age < self.warning_frames + self.active_frames

    def draw(self, canvas: tk.Canvas) -> None:
        # self.rect 契约是 (x, y, width, height)，绘图 API 需要 (x1, y1, x2, y2)。
        x, y, width, height = self.rect
        x2, y2 = x + width, y + height
        if not self.is_active:
            canvas.create_rectangle(
                x,
                y,
                x2,
                y2,
                outline=LASER_WARNING_COLOR,
                width=3,
                dash=(8, 5),
            )
            if self.orientation == "horizontal":
                canvas.create_line(
                    x,
                    y + height / 2,
                    x2,
                    y + height / 2,
                    fill=LASER_WARNING_COLOR,
                    dash=(4, 8),
                )
            else:
                canvas.create_line(
                    x + width / 2,
                    y,
                    x + width / 2,
                    y2,
                    fill=LASER_WARNING_COLOR,
                    dash=(4, 8),
                )
        else:
            canvas.create_rectangle(x, y, x2, y2, fill=LASER_ACTIVE_COLOR, outline="#ffd1dc", width=2)
            if self.orientation == "horizontal":
                canvas.create_line(
                    0,
                    self.position,
                    WINDOW_WIDTH,
                    self.position,
                    fill="#fff0f4",
                    width=3,
                )
            else:
                canvas.create_line(
                    self.position,
                    FIELD_TOP,
                    self.position,
                    FIELD_BOTTOM,
                    fill="#fff0f4",
                    width=3,
                )


class Boss(EnemyTank):
    """非坦克造型的 Boss，拥有普通攻击和多种主动技能。"""

    def __init__(
        self,
        x: float,
        y: float,
        level: int,
        tier: str,
        regular_health: int,
        mutation: str | None = None,
        rng: random.Random | None = None,
    ) -> None:
        self.boss_tier = "mega" if tier == "mega" else "mini"
        self.multiplier = (
            MEGA_BOSS_HEALTH_MULTIPLIER
            if self.boss_tier == "mega"
            else MINI_BOSS_HEALTH_MULTIPLIER
        )
        boss_health = regular_health * self.multiplier
        super().__init__(x, y, level, health=boss_health, guard=False, rng=rng)
        self.is_boss = True
        self.width = 104 if self.boss_tier == "mega" else 72
        self.height = 92 if self.boss_tier == "mega" else 68
        self.color = MEGA_BOSS_COLOR if self.boss_tier == "mega" else MINI_BOSS_COLOR
        self.core_color = MEGA_BOSS_CORE if self.boss_tier == "mega" else MINI_BOSS_CORE
        self.label = "熔核巨兽" if self.boss_tier == "mega" else "虚空猎杀号"
        self.boss_name = self.label
        self.normal_fire_timer = 80
        self.skill_timer = 115 if self.boss_tier == "mini" else 95
        self.skill_index = 0
        self.shield_timer = 0
        self.skill_names = (
            ("冲刺突袭", "环形冲击波", "召唤护卫", "散射弹幕")
            if self.boss_tier == "mini"
            else ("熔核激光", "毁灭冲击波", "召唤军团", "扇形弹幕", "能量护盾")
        )
        self.phase = 1
        self.phase_thresholds = (0.50,) if self.boss_tier == "mini" else (0.70, 0.35)
        self.mutation = mutation if mutation in BOSS_MUTATION_INFO else "berserk_core"
        self.mutation_timer = 240
        self.last_phase_health_ratio = 1.0
        self.base_boss_speed = self.base_speed
        mutation_name = BOSS_MUTATION_INFO[self.mutation][0]
        self.label = f"{self.label}·{mutation_name}"
        self.boss_name = self.label

    def update_timers(self) -> None:
        super().update_timers()
        if self.normal_fire_timer > 0:
            self.normal_fire_timer -= 1
        if self.skill_timer > 0:
            self.skill_timer -= 1
        if self.shield_timer > 0:
            self.shield_timer -= 1
        if self.mutation_timer > 0:
            self.mutation_timer -= 1

    def desired_phase(self) -> int:
        ratio = self.health / max(1, self.max_health)
        phase = 1
        for threshold in self.phase_thresholds:
            if ratio <= threshold:
                phase += 1
        return phase

    def check_phase_transition(self, game: TankBattleGame) -> None:
        desired = self.desired_phase()
        if desired <= self.phase:
            return
        self.phase = desired
        self.move_speed_multiplier = 1.0 + 0.18 * (self.phase - 1)
        self.skill_timer = min(self.skill_timer, 42)
        self.normal_fire_timer = min(self.normal_fire_timer, 28)
        game.message = f"{self.boss_name} 进入第 {self.phase} 阶段！"
        game.message_timer = 120
        game.add_explosion(*self.center, large=True)
        game.hazards.append(
            ShockwaveHazard(
                self.center[0],
                self.center[1],
                240 + 70 * self.phase,
                5.2 + self.phase,
                damage=1 if self.boss_tier == "mini" else 2,
                owner=self,
            )
        )
        self.fire_fan(game, 3 + self.phase * 2, spread=0.75 + self.phase * 0.12, speed=5.0 + self.phase * 0.4)

    def update_mutation(self, game: TankBattleGame) -> None:
        if self.mutation == "reflective_shell" and self.mutation_timer <= 0:
            self.shield_timer = max(self.shield_timer, 150)
            self.mutation_timer = 360
            game.message = f"{self.boss_name} 展开反射护盾"
            game.message_timer = 80
        elif self.mutation == "box_devourer" and game.powerups:
            box = min(
                game.powerups,
                key=lambda item: (item.x - self.x) ** 2 + (item.y - self.y) ** 2,
            )
            dx = box.center[0] - self.center[0]
            dy = box.center[1] - self.center[1]
            if abs(dx) > abs(dy):
                self.direction = "right" if dx > 0 else "left"
            else:
                self.direction = "down" if dy > 0 else "up"
            move_x, move_y = DIRECTIONS[self.direction]
            game.try_move_tank(self, int(move_x), int(move_y))
            if rectangles_overlap(self.rect, box.rect):
                self.health = min(self.max_health, self.health + max(1, self.max_health // 20))
                game.powerups.remove(box)
                game.add_explosion(*box.center, large=False)
                game.message = f"{self.boss_name} 吞噬盲盒并恢复生命"
                game.message_timer = 90

    def take_damage(self, amount: int = 1) -> bool:
        if self.shield_timer > 0:
            amount = max(1, int(math.ceil(amount * 0.25)))
        return super().take_damage(amount)

    def normal_attack(self, game: TankBattleGame) -> None:
        target = self.choose_target(game)
        if target is None:
            return
        bullet = self.fire(
            bullet_speed=5.2 if self.boss_tier == "mini" else 6.2,
            damage=1 if self.boss_tier == "mini" else 2,
            homing=True,
            target=target,
            visual_kind="boss",
        )
        if bullet is not None:
            game.bullets.append(bullet)

    def use_skill(self, game: TankBattleGame, skill: str) -> None:
        target = self.choose_target(game)
        if skill == "冲刺突袭":
            if target is not None:
                dx = target.x - self.x
                dy = target.y - self.y
                direction = "right" if abs(dx) > abs(dy) and dx > 0 else "left" if abs(dx) > abs(dy) else "down" if dy > 0 else "up"
                self.direction = direction
                move_x, move_y = DIRECTIONS[direction]
                for _ in range(10):
                    proposed_rect = (
                        self.x + move_x * self.speed,
                        self.y + move_y * self.speed,
                        self.width,
                        self.height,
                    )
                    # try_move_tank 会阻止 Boss 与玩家重叠，因此在移动前
                    # 检查预定位置，确保冲刺真正能够命中目标。
                    if rectangles_overlap(proposed_rect, target.rect):
                        if target.spawn_protection <= 0:
                            game.damage_player(target, 1, source="Boss 冲刺")
                        break
                    if not game.try_move_tank(self, int(move_x), int(move_y)):
                        break
            game.add_explosion(*self.center, large=True)
        elif skill in ("环形冲击波", "毁灭冲击波"):
            radius = 330 if self.boss_tier == "mini" else 460
            game.hazards.append(
                ShockwaveHazard(
                    self.center[0],
                    self.center[1],
                    radius,
                    5.4 if self.boss_tier == "mini" else 7.0,
                    damage=1 if self.boss_tier == "mini" else 2,
                    owner=self,
                )
            )
        elif skill in ("召唤护卫", "召唤军团"):
            count = 2 if self.boss_tier == "mini" else 4
            count += min(3, game.level // 25)
            game.spawn_guard_enemies(count, self)
        elif skill == "散射弹幕":
            self.fire_fan(game, 5, spread=0.62, speed=5.3)
        elif skill == "熔核激光":
            if target is None:
                orientation = game.run_rng.choice(("horizontal", "vertical"))
                position = game.run_rng.randint(FIELD_TOP + 40, FIELD_BOTTOM - 40)
            else:
                dx = abs(target.center[0] - self.center[0])
                dy = abs(target.center[1] - self.center[1])
                orientation = "horizontal" if dx > dy else "vertical"
                position = target.center[1] if orientation == "horizontal" else target.center[0]
            game.hazards.append(LaserHazard(orientation, position, owner=self, damage=2))
        elif skill == "扇形弹幕":
            self.fire_fan(game, 9, spread=1.20, speed=6.0)
        elif skill == "能量护盾":
            self.shield_timer = 170

    def fire_fan(self, game: TankBattleGame, count: int, spread: float, speed: float) -> None:
        target = self.choose_target(game)
        if target is None:
            base_angle = -math.pi / 2
        else:
            tx, ty = target.center
            sx, sy = self.center
            base_angle = math.atan2(ty - sy, tx - sx)
        if count <= 1:
            angles = [base_angle]
        else:
            angles = [
                base_angle - spread / 2 + spread * index / (count - 1)
                for index in range(count)
            ]
        for angle in angles:
            muzzle_x = self.center[0] - BULLET_SIZE / 2
            muzzle_y = self.center[1] - BULLET_SIZE / 2
            game.bullets.append(
                Bullet(
                    owner=self,
                    team="enemy",
                    x=muzzle_x,
                    y=muzzle_y,
                    speed=speed,
                    damage=1 if self.boss_tier == "mini" else 2,
                    visual_kind="fan",
                    vector=(math.cos(angle), math.sin(angle)),
                )
            )

    def ai_update(self, game: TankBattleGame) -> None:
        if not self.alive:
            return
        self.check_phase_transition(game)
        self.update_mutation(game)
        target = self.choose_target(game)
        if target is not None:
            dx = target.x - self.x
            dy = target.y - self.y
            if abs(dx) > abs(dy):
                self.direction = "right" if dx > 0 else "left"
            else:
                self.direction = "down" if dy > 0 else "up"
            move_dx, move_dy = DIRECTIONS[self.direction]
            if game.run_rng.random() < 0.75:
                game.try_move_tank(self, int(move_dx), int(move_dy))

        if self.normal_fire_timer <= 0:
            self.normal_attack(game)
            normal_fire_cooldown = max(32, 92 - min(48, game.level * 2))
            normal_fire_cooldown = max(18, round(normal_fire_cooldown * (0.86 ** (self.phase - 1))))
            if self.mutation == "berserk_core":
                normal_fire_cooldown = max(14, round(normal_fire_cooldown * 0.78))
            if game.effect_active("rapid_fire", self):
                normal_fire_cooldown = rapid_fire_cooldown(normal_fire_cooldown)
            self.normal_fire_timer = normal_fire_cooldown
        if self.skill_timer <= 0:
            skill = self.skill_names[self.skill_index % len(self.skill_names)]
            self.skill_index += 1
            self.use_skill(game, skill)
            skill_cooldown = max(72, 150 - min(70, game.level * 2))
            skill_cooldown = max(45, round(skill_cooldown * (0.82 ** (self.phase - 1))))
            if self.mutation == "berserk_core":
                skill_cooldown = max(36, round(skill_cooldown * 0.80))
            self.skill_timer = skill_cooldown

    def draw(self, canvas: tk.Canvas) -> None:
        if not self.alive:
            return
        center_x, center_y = self.center
        radius_x = self.width / 2
        radius_y = self.height / 2

        canvas.create_oval(
            center_x - radius_x - 8,
            center_y - radius_y - 8,
            center_x + radius_x + 8,
            center_y + radius_y + 8,
            outline="#633b8f" if self.boss_tier == "mini" else "#8e263d",
            width=3,
        )
        if self.boss_tier == "mega":
            for angle in range(0, 360, 45):
                radians = math.radians(angle)
                tip_x = center_x + math.cos(radians) * (radius_x + 17)
                tip_y = center_y + math.sin(radians) * (radius_y + 17)
                side_x = center_x + math.cos(radians + 0.22) * radius_x
                side_y = center_y + math.sin(radians + 0.22) * radius_y
                other_x = center_x + math.cos(radians - 0.22) * radius_x
                other_y = center_y + math.sin(radians - 0.22) * radius_y
                canvas.create_polygon(
                    tip_x,
                    tip_y,
                    side_x,
                    side_y,
                    other_x,
                    other_y,
                    fill=self.color,
                    outline="#ffd166",
                )
            canvas.create_oval(
                center_x - radius_x,
                center_y - radius_y,
                center_x + radius_x,
                center_y + radius_y,
                fill=self.color,
                outline="#ffccd5",
                width=3,
            )
        else:
            points = []
            for index in range(12):
                angle = math.tau * index / 12
                current_radius = radius_x if index % 2 == 0 else radius_x * 0.72
                points.extend((center_x + math.cos(angle) * current_radius, center_y + math.sin(angle) * current_radius * 0.88))
            canvas.create_polygon(*points, fill=self.color, outline="#e7c8ff", width=3)

        # 能量环与核心，确保 Boss 明显不是坦克外形。
        ring_radius = radius_x * (0.70 + 0.05 * math.sin(self.game_frame / 8 if hasattr(self, "game_frame") else 0))
        canvas.create_oval(
            center_x - ring_radius,
            center_y - ring_radius * 0.75,
            center_x + ring_radius,
            center_y + ring_radius * 0.75,
            outline=self.core_color,
            width=4,
        )
        canvas.create_oval(
            center_x - radius_x * 0.38,
            center_y - radius_y * 0.38,
            center_x + radius_x * 0.38,
            center_y + radius_y * 0.38,
            fill=self.core_color,
            outline="#ffffff",
            width=3,
        )
        canvas.create_oval(
            center_x - radius_x * 0.14,
            center_y - radius_y * 0.14,
            center_x + radius_x * 0.14,
            center_y + radius_y * 0.14,
            fill="#ffffff",
            outline="",
        )
        if self.shield_timer > 0:
            canvas.create_oval(
                center_x - radius_x - 12,
                center_y - radius_y - 12,
                center_x + radius_x + 12,
                center_y + radius_y + 12,
                outline="#80ffdb",
                width=4,
                dash=(8, 4),
            )
        self.draw_health_bar(canvas, width=self.width + 22, y_offset=-18)
        canvas.create_text(
            center_x,
            self.y + self.height / 2 + 16,
            text=f"{self.boss_name}  技能{len(self.skill_names)}种",
            fill="#fff1a8",
            font=("Microsoft YaHei", 9, "bold"),
        )


class Explosion:
    def __init__(self, x: float, y: float, large: bool = True, rng: random.Random | None = None) -> None:
        rng = rng or random
        self.x = x
        self.y = y
        self.age = 0
        self.duration = 30 if large else 16
        self.max_radius = 29 if large else 14
        self.particles = [
            (rng.uniform(0, math.tau), rng.uniform(0.7, 2.2), rng.randint(2, 4))
            for _ in range(10 if large else 5)
        ]

    def update(self) -> bool:
        self.age += 1
        return self.age < self.duration

    def draw(self, canvas: tk.Canvas) -> None:
        progress = self.age / self.duration
        radius = self.max_radius * min(progress * 1.7, 1.0)
        fade = max(0.0, 1.0 - progress)
        if fade > 0.15:
            canvas.create_oval(
                self.x - radius,
                self.y - radius,
                self.x + radius,
                self.y + radius,
                fill=EXPLOSION_ORANGE,
                outline=EXPLOSION_YELLOW,
                width=2,
            )
            inner_radius = radius * 0.48
            canvas.create_oval(
                self.x - inner_radius,
                self.y - inner_radius,
                self.x + inner_radius,
                self.y + inner_radius,
                fill="#fff4b0",
                outline="",
            )
        for angle, speed, size in self.particles:
            distance = self.age * speed * 2.2
            particle_x = self.x + distance * math.cos(angle)
            particle_y = self.y + distance * math.sin(angle)
            particle_size = max(1, int(size * fade + 0.5))
            canvas.create_rectangle(
                particle_x - particle_size,
                particle_y - particle_size,
                particle_x + particle_size,
                particle_y + particle_size,
                fill=EXPLOSION_YELLOW,
                outline="",
            )


class TankBattleGame:
    """包含菜单、剧情/无尽模式、双人和 Boss 系统的主控制器。"""

    @staticmethod
    def save_path() -> Path:
        return Path(__file__).with_name(SAVE_FILE_NAME)

    @classmethod
    def load_records(cls) -> dict[str, int]:
        defaults = {
            "high_score": 0,
            "highest_endless_level": 0,
            "best_combo": 0,
            "story_victories": 0,
            "last_seed": 0,
        }
        result = dict(defaults)
        try:
            loaded = json.loads(cls.save_path().read_text(encoding="utf-8"))
        except (OSError, ValueError, TypeError):
            return result
        if not isinstance(loaded, dict):
            return result
        for key, default in defaults.items():
            value = loaded.get(key, default)
            if isinstance(value, bool):
                result[key] = int(value)
                continue
            if isinstance(value, (int, float)):
                try:
                    result[key] = int(value)
                except (ValueError, OverflowError):
                    result[key] = default
            else:
                result[key] = default
        return result

    def save_records(self) -> None:
        try:
            self.save_path().write_text(
                json.dumps(self.records, ensure_ascii=False, indent=2),
                encoding="utf-8",
            )
        except (OSError, ValueError, TypeError):
            pass

    def update_records(self, final_state: str) -> None:
        self.records["high_score"] = max(self.records["high_score"], self.score)
        self.records["best_combo"] = max(self.records["best_combo"], self.best_combo)
        self.records["last_seed"] = self.run_seed
        if self.game_mode == "endless":
            self.records["highest_endless_level"] = max(
                self.records["highest_endless_level"],
                self.level,
            )
        elif final_state == "victory":
            self.records["story_victories"] += 1
        self.save_records()

    def __init__(self, root: tk.Tk | None = None) -> None:
        self.root = root if root is not None else tk.Tk()
        self.root.title("坦克大战 - Tkinter版")
        self.root.resizable(False, False)
        self.root.configure(bg=HUD_BACKGROUND)
        self.canvas = tk.Canvas(
            self.root,
            width=WINDOW_WIDTH,
            height=WINDOW_HEIGHT,
            bg=FIELD_BACKGROUND,
            highlightthickness=0,
        )
        self.canvas.pack()

        self.keys: set[str] = set()
        self.key_order: list[str] = []
        self.menu_buttons: list[dict[str, object]] = []
        self.hover_button = -1
        self.closed = False
        self.frame = 0

        self.state = "main_menu"
        self.player_mode = "single"
        self.game_mode = "story"
        self.mode = self.player_mode  # 兼容旧代码
        self.level = 1
        self.wave = 1  # 兼容旧代码
        self.score = 0
        self.lives = PLAYER_LIVES
        self.message = ""
        self.message_timer = 0
        self.intro_timer = 0
        self.countdown_timer = 0
        self.wave_active = False
        self.end_reason = ""
        self.run_seed = random.randint(100000, 999999)
        self.run_rng = random.Random(self.run_seed)
        self.upgrade_queue: list[PlayerTank] = []
        self.upgrade_choices: list[UpgradeDefinition] = []
        self.upgrade_player_index = 0
        self.pending_next_level = 1
        self.pending_rare_upgrade = False
        self.selected_challenges: set[str] = set()
        self.redeemed_codes: set[str] = set()
        self.code_invincible_active = False
        self.code_base_wall_active = False
        self.pending_run_seed = random.randint(100000, 999999)
        self.requested_run_seed: int | None = None
        self.challenge_score_multiplier = 1.0
        self.base_hits_remaining = 1
        self.records = self.load_records()
        self.current_map_key = "fortress"
        self.current_map_name = "边境要塞"
        self.objective_type = "elimination"
        self.objective_timer = 0
        self.objective_progress = 0
        self.objective_target = 0
        self.objective_complete = False
        self.ace_target: EnemyTank | None = None
        self.reinforcement_timer = 0
        self.enemy_boxes_taken = 0
        self.combo_count = 0
        self.combo_timer = 0
        self.best_combo = 0
        self.combo_reward_progress = 0
        self.run_statistics = {
            "kills": 0,
            "elite_kills": 0,
            "bosses": 0,
            "blind_boxes": 0,
            "damage_taken": 0,
            "levels_cleared": 0,
        }
        self.players: list[PlayerTank] = []
        self.player: PlayerTank | None = None
        self.active_effects: dict[str, int] = {}
        self.enemies: list[EnemyTank] = []
        self.bullets: list[Bullet] = []
        self.hazards: list[Hazard] = []
        self.powerups: list[PowerUp] = []
        self.explosions: list[Explosion] = []
        self.walls: list[Wall] = []
        self.base: Base | None = None
        self.drop_timer = DROP_INTERVAL_FRAMES

        self.root.bind_all("<KeyPress>", self.on_key_press)
        self.root.bind_all("<KeyRelease>", self.on_key_release)
        self.root.bind_all("<Button-1>", self.on_mouse_click)
        self.root.bind_all("<Motion>", self.on_mouse_move)
        self.root.protocol("WM_DELETE_WINDOW", self.close)
        self.canvas.focus_set()
        self.root.after(100, self.canvas.focus_set)
        self.render()
        self.root.after(FRAME_MS, self.game_loop)

    # =========================
    # 菜单
    # =========================
    def draw_menu_background(self) -> None:
        self.canvas.create_rectangle(0, 0, WINDOW_WIDTH, WINDOW_HEIGHT, fill=FIELD_BACKGROUND, outline="")
        for x in range(0, WINDOW_WIDTH + 1, 45):
            self.canvas.create_line(x, 0, x, WINDOW_HEIGHT, fill="#1b3039")
        for y in range(0, WINDOW_HEIGHT + 1, 45):
            self.canvas.create_line(0, y, WINDOW_WIDTH, y, fill="#1b3039")
        for center_x, center_y, radius in (
            (110, 125, 54),
            (790, 150, 70),
            (120, 610, 75),
            (790, 590, 55),
        ):
            self.canvas.create_oval(
                center_x - radius,
                center_y - radius,
                center_x + radius,
                center_y + radius,
                outline="#244854",
                width=2,
            )
            self.canvas.create_oval(
                center_x - radius / 2,
                center_y - radius / 2,
                center_x + radius / 2,
                center_y + radius / 2,
                outline="#1e3d48",
            )

    def draw_button(
        self,
        x: float,
        y: float,
        width: float,
        height: float,
        text: str,
        action: str,
        subtitle: str = "",
    ) -> None:
        index = len(self.menu_buttons)
        hovered = index == self.hover_button
        fill = MENU_BUTTON_HOVER if hovered else MENU_BUTTON
        outline = "#a5f0ea" if hovered else MENU_ACCENT
        self.canvas.create_rectangle(x, y, x + width, y + height, fill=fill, outline=outline, width=2)
        self.canvas.create_text(
            x + width / 2,
            y + height / 2 - (8 if subtitle else 0),
            text=text,
            fill=TEXT_COLOR,
            font=("Microsoft YaHei", 18 if not subtitle else 16, "bold"),
        )
        if subtitle:
            self.canvas.create_text(
                x + width / 2,
                y + height / 2 + 22,
                text=subtitle,
                fill="#c1e7e6",
                font=("Microsoft YaHei", 9),
            )
        self.menu_buttons.append(
            {"x1": x, "y1": y, "x2": x + width, "y2": y + height, "action": action}
        )

    def draw_main_menu(self) -> None:
        self.draw_menu_background()
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            150,
            text="坦克大战",
            fill=TEXT_COLOR,
            font=("Microsoft YaHei", 48, "bold"),
        )
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            204,
            text="TANK BATTLE",
            fill=MENU_ACCENT,
            font=("Arial", 16, "bold"),
        )
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            250,
            text="守护基地，构筑火力，挑战不断进化的敌军！",
            fill=MUTED_TEXT,
            font=("Microsoft YaHei", 13),
        )
        reward_text = self.message or self.redeemed_reward_summary()
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            286,
            text=reward_text,
            fill="#ffe082" if self.message else MUTED_TEXT,
            font=("Microsoft YaHei", 11, "bold" if self.message else "normal"),
        )
        self.draw_button(300, 320, 300, 64, "开始游戏", "player_mode_select", "选择人数、模式和挑战规则")
        self.draw_button(300, 404, 300, 58, "兑换码", "redeem_code", "本次启动有效，关闭游戏后失效")
        self.draw_button(300, 480, 300, 58, "退出游戏", "quit")
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            590,
            text="Enter：开始游戏        C：兑换码        Esc：退出",
            fill=MUTED_TEXT,
            font=("Microsoft YaHei", 11),
        )
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            625,
            text="剧情模式：固定关卡    无尽模式：随机目标、升级构筑与 Boss",
            fill="#668b92",
            font=("Microsoft YaHei", 10),
        )

    def redeemed_reward_summary(self) -> str:
        rewards: list[str] = []
        if getattr(self, "code_invincible_active", False):
            rewards.append("玩家永久无敌")
        if getattr(self, "code_base_wall_active", False):
            rewards.append("基地永久钢墙")
        if rewards:
            return f"已激活兑换奖励：{'、'.join(rewards)}（本次启动有效）"
        return "兑换码奖励只在本次启动内生效，关闭并重新打开游戏后会消失"

    def prompt_redeem_code(self) -> None:
        if self.closed:
            return
        self.keys.clear()
        self.key_order.clear()
        code = simpledialog.askstring("兑换码", "请输入兑换码：", parent=self.root)
        if code is None:
            self.canvas.focus_set()
            return
        self.redeem_code(code)
        self.canvas.focus_set()
        self.render()

    def redeem_code(self, raw_code: str) -> bool:
        code = raw_code.strip().lower()
        if not code:
            self.message = "兑换码不能为空"
            self.message_timer = 120
            return False
        if code == "wdwdwd123":
            self.redeemed_codes.add(code)
            if self.code_invincible_active:
                self.message = "兑换码奖励已激活：玩家永久无敌"
            else:
                self.code_invincible_active = True
                self.message = "兑换成功：玩家获得本次启动内永久无敌"
            self.message_timer = 160
            return True
        if code == "fqfqfq123":
            self.redeemed_codes.add(code)
            if self.code_base_wall_active:
                self.message = "兑换码奖励已激活：基地永久钢墙"
            else:
                self.code_base_wall_active = True
                self.add_redeemed_base_wall()
                self.message = "兑换成功：基地周围生成本次启动内永久钢墙"
            self.message_timer = 160
            return True
        self.message = "兑换码无效"
        self.message_timer = 120
        return False

    def add_redeemed_base_wall(self) -> None:
        if not getattr(self, "code_base_wall_active", False) or self.base is None:
            return
        self.walls = [wall for wall in self.walls if not getattr(wall, "unbreakable", False)]
        base_x, base_y, base_width, base_height = self.base.rect
        bottom_y = min(base_y + base_height, FIELD_BOTTOM - 8)
        wall_specs = (
            (base_x - 26, base_y - 26, base_width + 52, 18),
            (base_x - 26, base_y - 8, 18, base_height + 34),
            (base_x + base_width + 8, base_y - 8, 18, base_height + 34),
            (base_x - 26, bottom_y, base_width + 52, 8),
        )
        for x, y, width, height in wall_specs:
            x = max(FIELD_PADDING, min(float(x), WINDOW_WIDTH - width - FIELD_PADDING))
            y = max(FIELD_TOP + FIELD_PADDING, min(float(y), FIELD_BOTTOM - height))
            self.walls.append(
                Wall(x, y, width, height, "steel", hp=999999, unbreakable=True)
            )

    def draw_player_mode_select(self) -> None:
        self.draw_menu_background()
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            100,
            text="选择玩家人数",
            fill=TEXT_COLOR,
            font=("Microsoft YaHei", 32, "bold"),
        )
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            145,
            text="双人模式中两名玩家会分别构筑自己的升级流派",
            fill=MUTED_TEXT,
            font=("Microsoft YaHei", 12),
        )
        self.draw_button(105, 220, 300, 150, "单人模式", "choose_player_single", "P1：WASD/方向键 + 空格")
        self.draw_button(495, 220, 300, 150, "双人模式", "choose_player_double", "P1：WASD+空格   P2：方向键+Enter")
        self.draw_button(330, 535, 240, 52, "返回主菜单", "main_menu")

    def draw_game_mode_select(self) -> None:
        self.draw_menu_background()
        player_text = "单人作战" if self.player_mode == "single" else "双人协作"
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            95,
            text="选择游戏模式",
            fill=TEXT_COLOR,
            font=("Microsoft YaHei", 32, "bold"),
        )
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            140,
            text=f"当前人数：{player_text}",
            fill=MUTED_TEXT,
            font=("Microsoft YaHei", 12),
        )
        self.draw_button(95, 210, 320, 175, "剧情模式", "choose_story", "固定 3 关，体验基础构筑流程")
        self.draw_button(485, 210, 320, 175, "无尽模式", "choose_endless", "随机目标、精英、契约与无限关卡")
        self.canvas.create_rectangle(150, 420, 750, 490, fill=MENU_PANEL, outline=MENU_PANEL_LIGHT, width=2)
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            445,
            text="无尽模式 Boss 规则",
            fill="#ffd166",
            font=("Microsoft YaHei", 12, "bold"),
        )
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            470,
            text="每 10 关小 Boss · 每 100 关大 Boss · Boss 后保证稀有升级",
            fill=MUTED_TEXT,
            font=("Microsoft YaHei", 10),
        )
        self.draw_button(330, 545, 240, 52, "返回人数选择", "player_mode_select")

    def draw_challenge_select(self) -> None:
        self.draw_menu_background()
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            62,
            text="无尽模式 · 挑战契约",
            fill=TEXT_COLOR,
            font=("Microsoft YaHei", 30, "bold"),
        )
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            102,
            text="最多选择 2 条；难度越高，结算分数倍率越高",
            fill=MUTED_TEXT,
            font=("Microsoft YaHei", 11),
        )
        positions = ((80, 145), (330, 145), (580, 145), (205, 285), (455, 285))
        for index, (key, (name, description, multiplier)) in enumerate(CHALLENGE_INFO.items()):
            x, y = positions[index]
            selected = key in self.selected_challenges
            label = f"✓ {name}" if selected else name
            subtitle = f"{description} · ×{multiplier:.2f}"
            self.draw_button(x, y, 220, 105, label, f"toggle_challenge_{key}", subtitle)
        total_multiplier = math.prod(
            CHALLENGE_INFO[key][2] for key in self.selected_challenges
        ) if self.selected_challenges else 1.0
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            425,
            text=f"种子：{self.pending_run_seed}    已选 {len(self.selected_challenges)}/2    总倍率 ×{total_multiplier:.2f}",
            fill="#ffe082",
            font=("Microsoft YaHei", 13, "bold"),
        )
        self.draw_button(165, 485, 180, 56, "重掷种子", "reroll_seed")
        self.draw_button(360, 485, 180, 56, "开始挑战", "start_endless_challenge")
        self.draw_button(555, 485, 180, 56, "返回", "game_mode_select")
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            590,
            text="数字 1～5：选择契约    Enter：开始    Esc：返回",
            fill=MUTED_TEXT,
            font=("Microsoft YaHei", 10),
        )

    def show_player_mode_select(self) -> None:
        self.keys.clear()
        self.key_order.clear()
        self.state = "player_mode_select"
        self.hover_button = -1
        self.render()

    def show_mode_select(self) -> None:
        self.show_player_mode_select()

    def show_game_mode_select(self) -> None:
        self.keys.clear()
        self.key_order.clear()
        self.state = "game_mode_select"
        self.hover_button = -1
        self.render()

    def show_challenge_select(self) -> None:
        self.keys.clear()
        self.key_order.clear()
        self.state = "challenge_select"
        self.selected_challenges.clear()
        self.pending_run_seed = random.randint(100000, 999999)
        self.hover_button = -1
        self.render()

    def toggle_challenge(self, key: str) -> None:
        if key not in CHALLENGE_INFO:
            return
        if key in self.selected_challenges:
            self.selected_challenges.remove(key)
        elif len(self.selected_challenges) < 2:
            self.selected_challenges.add(key)
        else:
            self.message = "最多只能选择 2 条挑战契约"
            self.message_timer = 90
        self.hover_button = -1
        self.render()

    def return_to_menu(self) -> None:
        self.keys.clear()
        self.key_order.clear()
        self.state = "main_menu"
        self.hover_button = -1
        self.players.clear()
        self.player = None
        self.enemies.clear()
        self.bullets.clear()
        self.hazards.clear()
        self.powerups.clear()
        self.explosions.clear()
        self.walls.clear()
        self.base = None
        self.render()

    def on_mouse_move(self, event: tk.Event) -> None:
        if self.state not in (
            "main_menu",
            "player_mode_select",
            "game_mode_select",
            "upgrade_select",
            "challenge_select",
            "victory",
            "game_over",
        ):
            return
        new_hover = -1
        for index, button in enumerate(self.menu_buttons):
            if (
                float(button["x1"]) <= event.x <= float(button["x2"])
                and float(button["y1"]) <= event.y <= float(button["y2"])
            ):
                new_hover = index
                break
        if new_hover != self.hover_button:
            self.hover_button = new_hover
            self.render()

    def on_mouse_click(self, event: tk.Event) -> None:
        if self.state not in (
            "main_menu",
            "player_mode_select",
            "game_mode_select",
            "upgrade_select",
            "challenge_select",
            "victory",
            "game_over",
        ):
            return
        for button in self.menu_buttons:
            if (
                float(button["x1"]) <= event.x <= float(button["x2"])
                and float(button["y1"]) <= event.y <= float(button["y2"])
            ):
                self.activate_menu_action(str(button["action"]))
                return

    def activate_menu_action(self, action: str) -> None:
        if action == "player_mode_select":
            self.show_player_mode_select()
        elif action == "choose_player_single":
            self.player_mode = "single"
            self.mode = self.player_mode
            self.show_game_mode_select()
        elif action == "choose_player_double":
            self.player_mode = "double"
            self.mode = self.player_mode
            self.show_game_mode_select()
        elif action == "choose_story":
            self.start_game(self.player_mode, "story")
        elif action == "choose_endless":
            self.show_challenge_select()
        elif action.startswith("toggle_challenge_"):
            self.toggle_challenge(action.removeprefix("toggle_challenge_"))
        elif action == "reroll_seed":
            self.pending_run_seed = random.randint(100000, 999999)
            self.render()
        elif action == "start_endless_challenge":
            self.start_game(self.player_mode, "endless", run_seed=self.pending_run_seed)
        elif action == "redeem_code":
            self.prompt_redeem_code()
        elif action.startswith("choose_upgrade_"):
            try:
                choice_index = int(action.rsplit("_", 1)[1])
            except (TypeError, ValueError):
                return
            self.choose_upgrade(choice_index)
        elif action == "game_mode_select":
            self.show_game_mode_select()
        elif action == "main_menu":
            self.return_to_menu()
        elif action == "restart":
            self.start_game(self.player_mode, self.game_mode)
        elif action == "quit":
            self.close()

    # =========================
    # 游戏初始化与关卡流程
    # =========================
    def start_game(
        self,
        player_mode: str = "single",
        game_mode: str | None = None,
        run_seed: int | None = None,
    ) -> None:
        if player_mode in ("single", "double"):
            self.player_mode = player_mode
        if game_mode in ("story", "endless"):
            self.game_mode = game_mode
        elif game_mode is None:
            self.game_mode = "endless" if player_mode == "endless" else "story"
        if self.game_mode == "story":
            self.selected_challenges.clear()
        self.requested_run_seed = int(run_seed) if run_seed is not None else None
        self.mode = self.player_mode
        self.reset_battle()
        self.render()

    def reset_game(self) -> None:
        self.reset_battle()
        self.render()

    def reset_battle(self) -> None:
        self.keys.clear()
        self.key_order.clear()
        self.frame = 0
        self.state = "level_intro"
        self.run_seed = (
            self.requested_run_seed
            if self.requested_run_seed is not None
            else random.randint(100000, 999999)
        )
        self.requested_run_seed = None
        self.run_rng = random.Random(self.run_seed)
        self.challenge_score_multiplier = math.prod(
            CHALLENGE_INFO[key][2]
            for key in self.selected_challenges
            if key in CHALLENGE_INFO
        ) if self.selected_challenges else 1.0
        self.base_hits_remaining = 2 if "base_alarm" in self.selected_challenges else 1
        self.upgrade_queue = []
        self.upgrade_choices = []
        self.upgrade_player_index = 0
        self.pending_next_level = 1
        self.pending_rare_upgrade = False
        self.level = 1
        self.wave = 1
        self.score = 0
        self.message = ""
        self.message_timer = 0
        self.intro_timer = 0
        self.countdown_timer = 0
        self.wave_active = False
        self.end_reason = ""
        self.objective_type = "elimination"
        self.objective_timer = 0
        self.objective_progress = 0
        self.objective_target = 0
        self.objective_complete = False
        self.ace_target = None
        self.reinforcement_timer = 0
        self.enemy_boxes_taken = 0
        self.combo_count = 0
        self.combo_timer = 0
        self.best_combo = 0
        self.combo_reward_progress = 0
        self.run_statistics = {
            "kills": 0,
            "elite_kills": 0,
            "bosses": 0,
            "blind_boxes": 0,
            "damage_taken": 0,
            "levels_cleared": 0,
        }
        self.bullets = []
        self.hazards = []
        self.enemies = []
        self.explosions = []
        self.powerups = []
        self.drop_timer = DROP_INTERVAL_FRAMES
        self.walls = []
        self.base = Base(WINDOW_WIDTH / 2 - 24, WINDOW_HEIGHT - 54)

        spawn_y = FIELD_BOTTOM - TANK_SIZE - 5
        p1_move_keys = (
            {"w", "a", "s", "d", "up", "down", "left", "right"}
            if self.player_mode == "single"
            else {"w", "a", "s", "d"}
        )
        p1 = PlayerTank(1, WINDOW_WIDTH / 2 - 130, spawn_y, PLAYER_COLOR, p1_move_keys, {"space"})
        self.players = [p1]
        if self.player_mode == "double":
            self.players.append(
                PlayerTank(
                    2,
                    WINDOW_WIDTH / 2 + 94,
                    spawn_y,
                    PLAYER_TWO_COLOR,
                    {"up", "down", "left", "right"},
                    {"return", "kp_enter"},
                )
            )
        if "one_life" in self.selected_challenges:
            for player in self.players:
                player.lives = 1
        self.player = self.players[0]
        self.active_effects = self.player.effects
        self.lives = self.player.lives
        self.prepare_level(1)

    def prepare_level(self, level: int) -> None:
        self.level = max(1, int(level))
        self.wave = self.level
        self.enemies.clear()
        self.bullets.clear()
        self.hazards.clear()
        self.powerups.clear()
        self.wave_active = True
        self.objective_complete = False
        self.objective_progress = 0
        self.objective_target = 0
        self.objective_timer = 0
        self.ace_target = None
        self.reinforcement_timer = 0
        self.enemy_boxes_taken = 0
        self.drop_timer = DROP_INTERVAL_FRAMES

        boss_tier = self.boss_tier_for_level(self.level)
        self.current_map_key = self.select_map_for_level(self.level, boss_tier)
        self.walls = self.create_map(self.current_map_key)
        self.add_redeemed_base_wall()
        self.prepare_players_for_level()

        regular_health = regular_enemy_health(self.level)
        if self.game_mode == "story":
            enemy_count = 2 + min(self.level, 4)
        else:
            enemy_count = regular_enemy_count(self.level)
        if "enemy_horde" in self.selected_challenges:
            enemy_count = math.ceil(enemy_count * 1.30)

        if boss_tier is not None:
            guard_count = max(2, min(8, enemy_count // 3 + 1))
            self.spawn_guard_enemies(guard_count)
            boss_width = 104 if boss_tier == "mega" else 72
            boss_height = 92 if boss_tier == "mega" else 68
            boss_x, boss_y = self.find_safe_position(
                (WINDOW_WIDTH / 2 - boss_width / 2, FIELD_TOP + 24),
                boss_width,
                boss_height,
            )
            self.enemies.append(
                Boss(
                    boss_x,
                    boss_y,
                    self.level,
                    boss_tier,
                    regular_health,
                    mutation=self.roll_boss_mutation(),
                    rng=self.run_rng,
                )
            )
        else:
            self.spawn_regular_enemies(enemy_count, regular_health)

        self.setup_level_objective(boss_tier)
        self.state = "level_intro"
        self.intro_timer = LEVEL_INTRO_FRAMES
        self.countdown_timer = 0
        self.message = self.level_announcement()
        self.message_timer = 0

    def spawn_wave(self) -> None:
        """兼容旧接口：生成当前关卡并进入播报。"""
        self.prepare_level(self.level)

    def level_announcement(self) -> str:
        objective_name = OBJECTIVE_INFO[self.objective_type][0]
        if self.game_mode == "endless":
            tier = self.boss_tier_for_level(self.level)
            if tier == "mega":
                return f"无尽模式 · 第 {self.level} 关 · 大 Boss 降临 · {self.current_map_name}"
            if tier == "mini":
                return f"无尽模式 · 第 {self.level} 关 · 小 Boss 来袭 · {self.current_map_name}"
            return f"无尽模式 · 第 {self.level} 关 · {objective_name} · {self.current_map_name}"
        return f"剧情模式 · 第 {self.level} 关 · {objective_name} · {self.current_map_name}"

    def setup_level_objective(
        self,
        boss_tier: str | None = None,
        objective_type: str | None = None,
    ) -> None:
        if boss_tier is not None:
            self.objective_type = "boss"
        elif objective_type in OBJECTIVE_INFO and objective_type != "boss":
            self.objective_type = objective_type
        elif self.game_mode == "story":
            story_objectives = ("elimination", "ace_hunt", "survival")
            self.objective_type = story_objectives[min(self.level - 1, len(story_objectives) - 1)]
        else:
            objective_pool = ["elimination", "survival", "ace_hunt", "blind_box_race"]
            self.objective_type = self.run_rng.choice(objective_pool)

        if self.objective_type == "survival":
            self.objective_timer = SURVIVAL_OBJECTIVE_FRAMES
            self.reinforcement_timer = max(90, round(420 - min(260, self.level * 8)))
        elif self.objective_type == "blind_box_race":
            self.objective_target = BLIND_BOX_OBJECTIVE_TARGET + min(2, self.level // 30)
            self.drop_timer = min(self.drop_timer, max(90, DROP_INTERVAL_FRAMES // 3))
        elif self.objective_type == "ace_hunt":
            if not self.enemies:
                x, y = self.find_safe_position((WINDOW_WIDTH / 2, FIELD_TOP + 80))
                self.enemies.append(
                    EnemyTank(
                        x,
                        y,
                        self.level,
                        archetype="heavy",
                        affixes=self.roll_enemy_affixes(self.level, force_elite=True),
                        rng=self.run_rng,
                    )
                )
            self.ace_target = max(self.enemies, key=lambda enemy: enemy.max_health)
            self.ace_target.is_objective_target = True
            self.ace_target.is_elite = True
            self.ace_target.label = f"王牌指挥官·{self.ace_target.profile.name}"
            self.ace_target.max_health = max(3, math.ceil(self.ace_target.max_health * 1.7))
            self.ace_target.health = self.ace_target.max_health
            self.ace_target.score_value = max(self.ace_target.score_value, 600)

    def objective_status_text(self) -> str:
        name, description = OBJECTIVE_INFO[self.objective_type]
        if self.objective_type == "survival":
            seconds = max(0, math.ceil(self.objective_timer * FRAME_MS / 1000))
            return f"{name}：剩余 {seconds} 秒"
        if self.objective_type == "blind_box_race":
            return f"{name}：{self.objective_progress}/{self.objective_target}（敌军抢走 {self.enemy_boxes_taken}）"
        if self.objective_type == "ace_hunt":
            status = "已击败" if self.ace_target is None or not self.ace_target.alive else f"生命 {self.ace_target.health}/{self.ace_target.max_health}"
            return f"{name}：{status}"
        return f"{name}：{description}"

    def update_level_objective(self) -> None:
        if self.state != "playing" or self.objective_complete:
            return
        if self.objective_type == "survival":
            self.objective_timer = max(0, self.objective_timer - 1)
            self.reinforcement_timer -= 1
            if self.reinforcement_timer <= 0 and self.objective_timer > 0:
                health = regular_enemy_health(self.level)
                self.spawn_regular_enemies(min(3, 1 + self.level // 20), health)
                self.reinforcement_timer = max(90, round(420 - min(260, self.level * 8)))
            if self.objective_timer <= 0:
                self.objective_complete = True
        elif self.objective_type == "ace_hunt":
            self.objective_complete = self.ace_target is None or not self.ace_target.alive
        elif self.objective_type == "blind_box_race":
            if not self.powerups:
                self.drop_timer = min(self.drop_timer, max(90, DROP_INTERVAL_FRAMES // 3))
            self.objective_complete = self.objective_progress >= self.objective_target
        elif self.objective_type == "boss":
            self.objective_complete = self.current_boss() is None
        else:
            self.objective_complete = not self.enemies

    def boss_tier_for_level(self, level: int) -> str | None:
        if self.game_mode != "endless":
            return None
        if level % MEGA_BOSS_INTERVAL == 0:
            return "mega"
        if level % MINI_BOSS_INTERVAL == 0:
            return "mini"
        return None

    def roll_boss_mutation(self) -> str:
        return self.run_rng.choice(tuple(BOSS_MUTATION_INFO))

    def boss_health_for_level(self, level: int, tier: str) -> int:
        health = regular_enemy_health(level)
        if tier == "mega":
            return health * MEGA_BOSS_HEALTH_MULTIPLIER
        if tier == "mini":
            return health * MINI_BOSS_HEALTH_MULTIPLIER
        return health

    def prepare_players_for_level(self) -> None:
        for player in self.players:
            if player.lives <= 0:
                player.alive = False
                continue
            player.alive = True
            player.respawn_timer = 0
            player.x, player.y = self.find_player_spawn_position(player)
            player.direction = "up"
            player.spawn_protection = max(player.spawn_protection, 100)
            player.reset_level_resources()
            self.refresh_tank_speed(player)

    def available_upgrades(self, player: PlayerTank) -> list[UpgradeDefinition]:
        return [
            definition
            for definition in UPGRADE_DEFINITIONS.values()
            if player.upgrade_level(definition.key) < definition.max_level
        ]

    def roll_upgrade_choices(
        self,
        player: PlayerTank,
        rare_guaranteed: bool = False,
    ) -> list[UpgradeDefinition]:
        candidates = self.available_upgrades(player)
        if not candidates:
            return []
        choices: list[UpgradeDefinition] = []
        if rare_guaranteed:
            rare = [definition for definition in candidates if definition.rarity == "rare"]
            if rare:
                choices.append(self.run_rng.choice(rare))
        remaining = [definition for definition in candidates if definition not in choices]
        self.run_rng.shuffle(remaining)
        choices.extend(remaining[: max(0, UPGRADE_CHOICE_COUNT - len(choices))])
        return choices

    def current_upgrade_player(self) -> PlayerTank | None:
        if 0 <= self.upgrade_player_index < len(self.upgrade_queue):
            return self.upgrade_queue[self.upgrade_player_index]
        return None

    def begin_upgrade_selection(self, next_level: int, rare_guaranteed: bool = False) -> None:
        self.pending_next_level = max(1, int(next_level))
        self.pending_rare_upgrade = rare_guaranteed
        self.upgrade_queue = [player for player in self.players if player.lives > 0]
        self.upgrade_player_index = 0
        self.bullets.clear()
        self.hazards.clear()
        self.powerups.clear()
        if not self.upgrade_queue:
            self.prepare_level(self.pending_next_level)
            return
        player = self.current_upgrade_player()
        self.upgrade_choices = self.roll_upgrade_choices(player, rare_guaranteed) if player else []
        if not self.upgrade_choices:
            self.prepare_level(self.pending_next_level)
            return
        self.keys.clear()
        self.key_order.clear()
        self.hover_button = -1
        self.state = "upgrade_select"

    def choose_upgrade(self, choice_index: int) -> None:
        if self.state != "upgrade_select":
            return
        player = self.current_upgrade_player()
        if player is None or not (0 <= choice_index < len(self.upgrade_choices)):
            return
        chosen = self.upgrade_choices[choice_index]
        if not player.apply_upgrade(chosen.key):
            return
        self.message = f"{player.label} 获得局内升级：{chosen.name}"
        self.message_timer = 100
        self.upgrade_player_index += 1
        next_player = self.current_upgrade_player()
        if next_player is None:
            self.upgrade_choices = []
            self.prepare_level(self.pending_next_level)
            return
        self.upgrade_choices = self.roll_upgrade_choices(next_player, self.pending_rare_upgrade)
        if not self.upgrade_choices:
            self.prepare_level(self.pending_next_level)
            return
        self.hover_button = -1
        self.render()

    def enemy_archetype_pool(self, level: int) -> tuple[str, ...]:
        pool = ["standard"]
        if level >= 2:
            pool.extend(("scout", "standard"))
        if level >= 4:
            pool.append("heavy")
        if level >= 6:
            pool.append("sniper")
        if level >= 8:
            pool.append("demolisher")
        return tuple(pool)

    def roll_enemy_affixes(self, level: int, force_elite: bool = False) -> tuple[str, ...]:
        if level < 6 and not force_elite:
            return ()
        chance = min(0.38, 0.06 + level * 0.006)
        if "elite_invasion" in self.selected_challenges:
            chance = min(0.75, chance * 2)
        if not force_elite and self.run_rng.random() >= chance:
            return ()
        count = 2 if level >= 40 and self.run_rng.random() < 0.28 else 1
        return tuple(self.run_rng.sample(tuple(ELITE_AFFIX_INFO), k=count))

    def spawn_regular_enemies(self, count: int, health: int) -> None:
        spawn_points = [
            (70, FIELD_TOP + 25),
            (WINDOW_WIDTH / 2 - TANK_SIZE / 2, FIELD_TOP + 25),
            (WINDOW_WIDTH - 70 - TANK_SIZE, FIELD_TOP + 25),
            (150, FIELD_TOP + 130),
            (WINDOW_WIDTH - 190, FIELD_TOP + 130),
            (WINDOW_WIDTH / 2 - TANK_SIZE / 2, FIELD_TOP + 205),
            (75, FIELD_TOP + 300),
            (WINDOW_WIDTH - 110, FIELD_TOP + 300),
        ]
        archetype_pool = self.enemy_archetype_pool(self.level)
        for index in range(count):
            x, y = self.find_safe_position(spawn_points[index % len(spawn_points)], TANK_SIZE, TANK_SIZE)
            archetype = self.run_rng.choice(archetype_pool)
            affixes = self.roll_enemy_affixes(self.level)
            self.enemies.append(
                EnemyTank(
                    x,
                    y,
                    self.level,
                    health=health,
                    archetype=archetype,
                    affixes=affixes,
                    rng=self.run_rng,
                )
            )

    def spawn_guard_enemies(self, count: int, boss: Boss | None = None) -> None:
        health = regular_enemy_health(self.level)
        for index in range(max(0, count)):
            if boss is not None:
                angle = math.tau * index / max(1, count)
                preferred = (
                    boss.center[0] + math.cos(angle) * 130 - TANK_SIZE / 2,
                    boss.center[1] + math.sin(angle) * 100 - TANK_SIZE / 2,
                )
            else:
                preferred = (
                    self.run_rng.randint(35, WINDOW_WIDTH - 70),
                    self.run_rng.randint(FIELD_TOP + 20, FIELD_TOP + 170),
                )
            x, y = self.find_safe_position(preferred, TANK_SIZE, TANK_SIZE)
            affixes = self.roll_enemy_affixes(self.level, force_elite=self.level >= 30 and index == 0)
            self.enemies.append(
                EnemyTank(
                    x,
                    y,
                    self.level,
                    health=health,
                    guard=True,
                    archetype="guard",
                    affixes=affixes,
                    rng=self.run_rng,
                )
            )

    def update_pre_game(self) -> None:
        """播报与倒计时阶段只更新自身计时，不更新战斗对象。"""
        self.frame += 1
        if self.state == "level_intro":
            self.intro_timer -= 1
            if self.intro_timer <= 0:
                self.state = "countdown"
                self.countdown_timer = COUNTDOWN_TOTAL_FRAMES
        elif self.state == "countdown":
            self.countdown_timer -= 1
            if self.countdown_timer <= 0:
                self.countdown_timer = 0
                self.state = "playing"
                self.message = "开始战斗！"
                self.message_timer = 75

    def countdown_number(self) -> int:
        if self.countdown_timer <= 0:
            return 0
        return max(1, min(3, math.ceil(self.countdown_timer / COUNTDOWN_STEP_FRAMES)))

    def select_map_for_level(self, level: int, boss_tier: str | None = None) -> str:
        if boss_tier is not None:
            return "boss_arena"
        map_keys = ("fortress", "crossfire", "islands", "corridors", "ruins")
        return map_keys[(max(1, level) - 1 + self.run_seed) % len(map_keys)]

    def create_map(self, map_key: str | None = None) -> list[Wall]:
        map_key = map_key or self.current_map_key
        map_names = {
            "fortress": "边境要塞",
            "crossfire": "交叉火线",
            "islands": "钢铁群岛",
            "corridors": "回声走廊",
            "ruins": "破碎遗迹",
            "boss_arena": "核心竞技场",
        }
        self.current_map_name = map_names.get(map_key, "边境要塞")
        walls: list[Wall] = []

        def add_brick_row(start_x: int, y: int, count: int, spacing: int = 22) -> None:
            for index in range(count):
                walls.append(Wall(start_x + index * spacing, y, 20, 18, "brick"))

        def add_brick_column(x: int, start_y: int, count: int, spacing: int = 22) -> None:
            for index in range(count):
                walls.append(Wall(x, start_y + index * spacing, 18, 20, "brick"))

        def add_steel(x: int, y: int, width: int, height: int) -> None:
            walls.append(Wall(x, y, width, height, "steel", hp=999))

        if map_key == "crossfire":
            add_brick_row(100, 205, 7)
            add_brick_row(646, 205, 7)
            add_brick_row(100, 470, 7)
            add_brick_row(646, 470, 7)
            add_brick_column(325, 250, 7)
            add_brick_column(557, 250, 7)
            add_steel(410, 245, 80, 20)
            add_steel(410, 430, 80, 20)
        elif map_key == "islands":
            for x, y in ((120, 190), (340, 190), (560, 190), (230, 350), (450, 350), (670, 350)):
                add_brick_row(x, y, 4)
                add_brick_row(x, y + 44, 4)
            add_steel(410, 285, 80, 24)
            add_steel(410, 505, 80, 20)
        elif map_key == "corridors":
            add_brick_column(160, 180, 15)
            add_brick_column(350, 145, 14)
            add_brick_column(540, 180, 15)
            add_brick_column(730, 145, 14)
            add_steel(250, 320, 50, 20)
            add_steel(600, 420, 50, 20)
        elif map_key == "ruins":
            for x, y, count in (
                (80, 180, 5),
                (310, 160, 4),
                (620, 190, 7),
                (160, 330, 6),
                (500, 360, 5),
                (70, 500, 8),
                (610, 510, 6),
            ):
                add_brick_row(x, y, count)
            add_steel(390, 255, 120, 20)
            add_steel(270, 455, 70, 20)
            add_steel(560, 455, 70, 20)
        elif map_key == "boss_arena":
            add_steel(105, 285, 90, 20)
            add_steel(705, 285, 90, 20)
            add_steel(105, 490, 90, 20)
            add_steel(705, 490, 90, 20)
            add_brick_row(335, 510, 4)
            add_brick_row(485, 510, 4)
        else:
            add_brick_row(105, 150, 5)
            add_brick_row(685, 150, 5)
            add_brick_column(245, 205, 3)
            add_brick_column(637, 205, 3)
            add_brick_row(325, 255, 4)
            add_brick_row(485, 255, 4)
            add_brick_row(80, 340, 4)
            add_brick_row(742, 340, 4)
            add_brick_row(165, 440, 5)
            add_brick_row(625, 440, 5)
            add_brick_row(340, 530, 3)
            add_brick_row(494, 530, 3)
            for x, y, width, height in (
                (410, 150, 80, 20),
                (410, 350, 80, 20),
                (270, 300, 20, 80),
                (610, 300, 20, 80),
                (300, 395, 70, 20),
                (530, 395, 70, 20),
            ):
                add_steel(x, y, width, height)
        return walls

    def find_safe_position(
        self,
        preferred: tuple[float, float],
        width: float = TANK_SIZE,
        height: float = TANK_SIZE,
        ignore: Tank | None = None,
    ) -> tuple[float, float]:
        """寻找不与墙、基地、玩家、敌人或道具重叠的位置。"""
        preferred_x, preferred_y = preferred
        candidates = [(preferred_x, preferred_y)]
        for offset in (40, -40, 80, -80, 120, -120, 180, -180):
            candidates.extend(((preferred_x + offset, preferred_y), (preferred_x, preferred_y + offset)))
        for y in range(int(FIELD_TOP + 10), int(FIELD_BOTTOM - height), 20):
            for x in range(20, int(WINDOW_WIDTH - width - 20), 20):
                candidates.append((x, y))

        for x, y in candidates:
            x = max(FIELD_PADDING, min(float(x), WINDOW_WIDTH - width - FIELD_PADDING))
            y = max(FIELD_TOP + FIELD_PADDING, min(float(y), FIELD_BOTTOM - height))
            candidate_rect = (x, y, width, height)
            if any(wall.alive and rectangles_overlap(candidate_rect, wall.rect) for wall in self.walls):
                continue
            if self.base is not None and self.base.alive and rectangles_overlap(candidate_rect, self.base.rect):
                continue
            if any(
                player is not ignore
                and player.alive
                and rectangles_overlap(candidate_rect, player.rect)
                for player in self.players
            ):
                continue
            if any(
                enemy is not ignore
                and enemy.alive
                and rectangles_overlap(candidate_rect, enemy.rect)
                for enemy in self.enemies
            ):
                continue
            if any(rectangles_overlap(candidate_rect, powerup.rect) for powerup in self.powerups):
                continue
            return x, y
        return (
            max(FIELD_PADDING, min(float(preferred_x), WINDOW_WIDTH - width - FIELD_PADDING)),
            max(FIELD_TOP + FIELD_PADDING, min(float(preferred_y), FIELD_BOTTOM - height)),
        )

    def find_spawn_position(self, preferred: tuple[float, float]) -> tuple[float, float]:
        """兼容旧接口。"""
        return self.find_safe_position(preferred, TANK_SIZE, TANK_SIZE)

    def find_player_spawn_position(self, player: PlayerTank) -> tuple[float, float]:
        return self.find_safe_position((player.spawn_x, player.spawn_y), TANK_SIZE, TANK_SIZE, player)

    # =========================
    # 输入与主循环
    # =========================
    def on_key_press(self, event: tk.Event) -> None:
        key = event.keysym.lower()
        was_down = key in self.keys
        self.keys.add(key)
        if not was_down:
            self.key_order.append(key)
        if was_down:
            return

        if self.state == "main_menu":
            if key in ("return", "space"):
                self.show_player_mode_select()
            elif key == "c":
                self.prompt_redeem_code()
            elif key == "escape":
                self.close()
            return
        if self.state == "player_mode_select":
            if key in ("1", "num1", "kp_1"):
                self.player_mode = "single"
                self.mode = self.player_mode
                self.show_game_mode_select()
            elif key in ("2", "num2", "kp_2"):
                self.player_mode = "double"
                self.mode = self.player_mode
                self.show_game_mode_select()
            elif key in ("escape", "backspace"):
                self.return_to_menu()
            return
        if self.state == "game_mode_select":
            if key in ("1", "num1", "kp_1"):
                self.start_game(self.player_mode, "story")
            elif key in ("2", "num2", "kp_2"):
                self.show_challenge_select()
            elif key in ("escape", "backspace"):
                self.show_player_mode_select()
            return
        if self.state == "challenge_select":
            challenge_keys = {
                "1": 0,
                "num1": 0,
                "kp_1": 0,
                "2": 1,
                "num2": 1,
                "kp_2": 1,
                "3": 2,
                "num3": 2,
                "kp_3": 2,
                "4": 3,
                "num4": 3,
                "kp_4": 3,
                "5": 4,
                "num5": 4,
                "kp_5": 4,
            }
            if key in challenge_keys:
                challenge_key = tuple(CHALLENGE_INFO)[challenge_keys[key]]
                self.toggle_challenge(challenge_key)
            elif key in ("return", "space"):
                self.start_game(self.player_mode, "endless", run_seed=self.pending_run_seed)
            elif key == "r":
                self.pending_run_seed = random.randint(100000, 999999)
                self.render()
            elif key in ("escape", "backspace"):
                self.show_game_mode_select()
            return
        if self.state == "upgrade_select":
            choice_keys = {
                "1": 0,
                "num1": 0,
                "kp_1": 0,
                "2": 1,
                "num2": 1,
                "kp_2": 1,
                "3": 2,
                "num3": 2,
                "kp_3": 2,
            }
            if key in choice_keys:
                self.choose_upgrade(choice_keys[key])
            elif key in ("escape", "m"):
                self.return_to_menu()
            return
        if self.state in ("level_intro", "countdown"):
            if key in ("escape", "m"):
                self.return_to_menu()
            return
        if key == "p" and self.state in ("playing", "paused"):
            self.toggle_pause()
        elif key == "r" and self.state in ("victory", "game_over"):
            self.start_game(self.player_mode, self.game_mode)
        elif key == "m" and self.state in ("playing", "paused", "victory", "game_over"):
            self.return_to_menu()
        elif key == "escape":
            self.close()

    def on_key_release(self, event: tk.Event) -> None:
        key = event.keysym.lower()
        self.keys.discard(key)
        while key in self.key_order:
            self.key_order.remove(key)

    def toggle_pause(self) -> None:
        if self.state == "playing":
            self.state = "paused"
        elif self.state == "paused":
            self.state = "playing"

    def close(self) -> None:
        if self.closed:
            return
        self.closed = True
        try:
            self.root.destroy()
        except tk.TclError:
            pass

    def game_loop(self) -> None:
        if self.closed:
            return
        if self.state == "playing":
            self.update()
        elif self.state in ("level_intro", "countdown"):
            self.update_pre_game()
        self.render()
        if not self.closed:
            self.root.after(FRAME_MS, self.game_loop)

    # =========================
    # 战斗更新
    # =========================
    def update(self) -> None:
        self.frame += 1
        if self.message_timer > 0:
            self.message_timer -= 1
        self.update_combo()

        self.update_effects()
        for player in self.players:
            player.update_timers()
            if player.respawn_timer > 0:
                player.respawn_timer -= 1
                if player.respawn_timer == 0 and player.lives > 0 and self.state == "playing":
                    self.respawn_player(player)

        self.update_players()
        for enemy in self.enemies[:]:
            enemy.game_frame = self.frame
            enemy.update_timers()
            self.refresh_tank_speed(enemy)
            if enemy.alive:
                enemy.ai_update(self)

        self.update_bullets()
        self.update_hazards()
        self.update_powerups()
        self.enemies = [enemy for enemy in self.enemies if enemy.alive]
        self.explosions = [explosion for explosion in self.explosions if explosion.update()]
        self.update_level_objective()
        self.check_level_progress()
        self.sync_legacy_aliases()

    def update_combo(self) -> None:
        if self.combo_timer <= 0:
            self.combo_count = 0
            self.combo_reward_progress = 0
            return
        self.combo_timer -= 1
        if self.combo_timer <= 0:
            self.combo_count = 0
            self.combo_reward_progress = 0

    def combo_multiplier(self) -> float:
        return 1.0 + min(2.0, (self.combo_count // 5) * 0.25)

    def register_combo_kill(self, killer: Tank | None, enemy: EnemyTank) -> None:
        self.combo_count = min(MAX_COMBO, self.combo_count + 1)
        bonus_window = 90 if enemy.is_elite or enemy.is_boss else 0
        self.combo_timer = COMBO_WINDOW_FRAMES + bonus_window
        self.best_combo = max(self.best_combo, self.combo_count)
        self.combo_reward_progress += 1
        if isinstance(killer, PlayerTank):
            target = getattr(enemy, "last_target", None)
            if isinstance(target, PlayerTank) and target is not killer:
                killer.stats["cover_kills"] += 1
                self.score += 75
        if self.combo_reward_progress >= COMBO_REWARD_KILLS:
            self.combo_reward_progress = 0
            x, y = self.find_powerup_position()
            self.powerups.append(PowerUp(BLIND_BOX_KIND, x, y))
            self.message = f"团队连击达到 {self.combo_count}，奖励盲盒已投放！"
            self.message_timer = 100

    def team_proximity_bonus(self, player: PlayerTank) -> float:
        for teammate in self.players:
            if teammate is player or not teammate.alive:
                continue
            if math.hypot(teammate.center[0] - player.center[0], teammate.center[1] - player.center[1]) <= 130:
                return 1.10
        return 1.0

    def player_direction_from_input(self, player: PlayerTank) -> str | None:
        for key in reversed(self.key_order):
            if key not in self.keys or key not in player.move_keys:
                continue
            if key in KEY_TO_DIRECTION:
                return KEY_TO_DIRECTION[key]
        # 兼容外部测试直接修改 keys 的情况。
        for key in player.move_keys:
            if key in self.keys and key in KEY_TO_DIRECTION:
                return KEY_TO_DIRECTION[key]
        return None

    def fire_player_weapon(self, player: PlayerTank) -> None:
        bounce_remaining, piercing = self.tank_bullet_traits(player)
        bullet = player.fire(
            bullet_speed=self.tank_bullet_speed(player),
            bounce_remaining=bounce_remaining,
            piercing=piercing,
            damage=player.bullet_damage,
            explosive=player.explosive_shots,
        )
        if bullet is None:
            return
        proximity_bonus = self.team_proximity_bonus(player)
        if proximity_bonus > 1.0:
            player.fire_cooldown = max(1, math.ceil(player.fire_cooldown / proximity_bonus))
        if not player.twin_shot:
            self.bullets.append(bullet)
            return

        base_angles = {
            "up": -math.pi / 2,
            "down": math.pi / 2,
            "left": math.pi,
            "right": 0.0,
        }
        base_angle = base_angles[player.direction]
        bullet.vx, bullet.vy = normalized_vector(
            math.cos(base_angle - 0.10),
            math.sin(base_angle - 0.10),
        )
        bullet.direction = vector_to_direction(bullet.vx, bullet.vy)
        second = Bullet(
            owner=player,
            team="player",
            x=bullet.x,
            y=bullet.y,
            speed=bullet.speed,
            bounce_remaining=bounce_remaining,
            piercing=piercing,
            damage=player.bullet_damage,
            vector=(math.cos(base_angle + 0.10), math.sin(base_angle + 0.10)),
            explosive=player.explosive_shots,
        )
        self.bullets.extend((bullet, second))

    def update_players(self) -> None:
        for player in self.players:
            if not player.alive:
                continue
            self.refresh_tank_speed(player)
            direction = self.player_direction_from_input(player)
            if direction is not None:
                if self.effect_active("reverse_controls", player):
                    direction = OPPOSITE_DIRECTIONS[direction]
                player.direction = direction
                dx, dy = DIRECTIONS[direction]
                self.try_move_tank(player, int(dx), int(dy))

            if any(key in self.keys for key in player.fire_keys):
                self.fire_player_weapon(player)

    def update_player(self) -> None:
        """兼容旧接口，更新所有玩家。"""
        self.update_players()

    def try_move_tank(self, tank: Tank, dx: int, dy: int) -> bool:
        if not tank.alive:
            return False
        new_x = tank.x + dx * tank.speed
        new_y = tank.y + dy * tank.speed
        new_x = max(FIELD_PADDING, min(new_x, WINDOW_WIDTH - tank.width - FIELD_PADDING))
        new_y = max(FIELD_TOP + FIELD_PADDING, min(new_y, FIELD_BOTTOM - tank.height))
        candidate = (new_x, new_y, tank.width, tank.height)
        if any(wall.alive and rectangles_overlap(candidate, wall.rect) for wall in self.walls):
            return False
        if self.base is not None and self.base.alive and rectangles_overlap(candidate, self.base.rect):
            return False
        for other in [*self.players, *self.enemies]:
            if other is tank or not other.alive:
                continue
            if rectangles_overlap(candidate, other.rect):
                return False
        moved = new_x != tank.x or new_y != tank.y
        if moved:
            tank.x = new_x
            tank.y = new_y
        return moved

    def update_bullets(self) -> None:
        for bullet in self.bullets:
            if not bullet.active:
                continue
            bullet.update()
            if not self.keep_bullet_in_field(bullet):
                continue

            if bullet.piercing:
                for wall in self.walls:
                    if (
                        wall.alive
                        and id(wall) not in bullet.hit_walls
                        and rectangles_overlap(bullet.rect, wall.rect)
                    ):
                        bullet.hit_walls.add(id(wall))
                        if wall.unbreakable:
                            self.hit_wall(wall, bullet)
                            break
                        self.damage_wall(wall, bullet)
            else:
                for wall in self.walls:
                    if wall.alive and rectangles_overlap(bullet.rect, wall.rect):
                        self.handle_wall_collision(wall, bullet)
                        break
            if not bullet.active:
                continue

            if self.base is not None and self.base.alive and rectangles_overlap(bullet.rect, self.base.rect):
                bullet.active = False
                if bullet.team == "enemy":
                    self.destroy_base()
                else:
                    self.add_explosion(*self.base_center(), large=False)
                continue

            if bullet.team == "player":
                for enemy in self.enemies:
                    if not enemy.alive or id(enemy) in bullet.hit_targets:
                        continue
                    if rectangles_overlap(bullet.rect, enemy.rect):
                        bullet.hit_targets.add(id(enemy))
                        killed = enemy.take_damage(bullet.damage)
                        if enemy.is_boss and isinstance(bullet.owner, PlayerTank):
                            bullet.owner.stats["boss_damage"] += min(bullet.damage, max(0, enemy.max_health))
                        self.add_explosion(*enemy.center, large=killed and enemy.is_boss)
                        if killed:
                            self.on_enemy_destroyed(enemy, killer=bullet.owner, explosive=bullet.explosive)
                        if not bullet.piercing:
                            bullet.active = False
                            break
            else:
                for player in self.players:
                    if (
                        player.alive
                        and player.spawn_protection <= 0
                        and rectangles_overlap(bullet.rect, player.rect)
                    ):
                        bullet.active = False
                        if bullet.explosive:
                            self.trigger_hostile_explosion(bullet.center, bullet.damage, source="爆破炮弹")
                        else:
                            self.damage_player(player, bullet.damage, source="炮弹")
                        break

        # 敌我炮弹相撞时一起消失。
        active_bullets = [bullet for bullet in self.bullets if bullet.active]
        for index, first in enumerate(active_bullets):
            for second in active_bullets[index + 1 :]:
                if (
                    first.active
                    and second.active
                    and first.team != second.team
                    and rectangles_overlap(first.rect, second.rect)
                ):
                    first.active = False
                    second.active = False
                    self.add_explosion(*first.center, large=False)
                    break
        self.bullets = [bullet for bullet in self.bullets if bullet.active]

    def keep_bullet_in_field(self, bullet: Bullet) -> bool:
        left = FIELD_PADDING
        right = WINDOW_WIDTH - FIELD_PADDING - BULLET_SIZE
        top = FIELD_TOP + FIELD_PADDING
        bottom = FIELD_BOTTOM - BULLET_SIZE
        if bullet.bounce_remaining > 0 and not bullet.piercing:
            bounced = False
            if bullet.x < left:
                bullet.x = left
                bullet.vx = abs(bullet.vx)
                bounced = True
            elif bullet.x > right:
                bullet.x = right
                bullet.vx = -abs(bullet.vx)
                bounced = True
            if bullet.y < top:
                bullet.y = top
                bullet.vy = abs(bullet.vy)
                bounced = True
            elif bullet.y > bottom:
                bullet.y = bottom
                bullet.vy = -abs(bullet.vy)
                bounced = True
            if bounced:
                bullet.bounce_remaining -= 1
                bullet.vx, bullet.vy = normalized_vector(bullet.vx, bullet.vy)
                bullet.direction = vector_to_direction(bullet.vx, bullet.vy)
                self.add_explosion(*bullet.center, large=False)
            return True
        if (
            bullet.x < -BULLET_SIZE
            or bullet.x > WINDOW_WIDTH
            or bullet.y < FIELD_TOP - BULLET_SIZE
            or bullet.y > FIELD_BOTTOM
        ):
            bullet.active = False
            return False
        return True

    def handle_wall_collision(self, wall: Wall, bullet: Bullet) -> None:
        if bullet.bounce_remaining > 0 and not bullet.piercing:
            self.damage_wall(wall, bullet)
            bullet.bounce_remaining -= 1
            bullet.reflect_from_wall(wall)
        else:
            self.hit_wall(wall, bullet)

    def move_bullet_out_of_wall(self, wall: Wall, bullet: Bullet) -> None:
        """兼容旧接口，使用统一反弹实现。"""
        bullet.reflect_from_wall(wall)

    def damage_wall(self, wall: Wall, bullet: Bullet) -> None:
        if wall.unbreakable:
            self.add_explosion(wall.x + wall.width / 2, wall.y + wall.height / 2, large=False)
            return
        if wall.kind == "brick":
            wall.hp -= 1
            if wall.hp <= 0:
                wall.alive = False
                if bullet.team == "player":
                    self.score += 5
        self.add_explosion(wall.x + wall.width / 2, wall.y + wall.height / 2, large=False)

    def hit_wall(self, wall: Wall, bullet: Bullet) -> None:
        bullet.active = False
        self.damage_wall(wall, bullet)
        if bullet.team == "enemy" and bullet.explosive:
            self.trigger_hostile_explosion(bullet.center, bullet.damage, source="爆破炮弹")

    def trigger_hostile_explosion(
        self,
        center: tuple[float, float],
        damage: int = 1,
        source: str = "爆炸",
        radius: float = 78,
    ) -> None:
        center_x, center_y = center
        self.add_explosion(center_x, center_y, large=True)
        for player in self.players:
            if not player.alive or player.spawn_protection > 0:
                continue
            if math.hypot(player.center[0] - center_x, player.center[1] - center_y) <= radius:
                self.damage_player(player, damage, source=source)
        for wall in self.walls:
            if not wall.alive or wall.unbreakable or wall.kind != "brick":
                continue
            wall_center = center_of(wall.rect)
            if math.hypot(wall_center[0] - center_x, wall_center[1] - center_y) <= radius:
                wall.hp -= 1
                if wall.hp <= 0:
                    wall.alive = False

    def on_enemy_destroyed(
        self,
        enemy: EnemyTank,
        killer: Tank | None = None,
        explosive: bool = False,
    ) -> None:
        self.register_combo_kill(killer, enemy)
        if enemy.is_boss:
            base_score = 5000 if getattr(enemy, "boss_tier", "mini") == "mini" else 50000
            self.hazards = [hazard for hazard in self.hazards if hazard.owner is not enemy]
            self.message = f"{getattr(enemy, 'boss_name', 'Boss')} 已被击败！"
            self.message_timer = 110
        else:
            base_score = getattr(enemy, "score_value", 100)
        self.score += round(
            base_score * self.combo_multiplier() * self.challenge_score_multiplier
        )
        self.run_statistics["kills"] += 1
        if isinstance(killer, PlayerTank):
            killer.stats["kills"] += 1
            if getattr(enemy, "is_elite", False):
                killer.stats["elite_kills"] += 1
                self.run_statistics["elite_kills"] += 1
        if explosive and not enemy.is_boss:
            self.trigger_explosive_warhead(enemy.center, killer)

    def trigger_explosive_warhead(
        self,
        center: tuple[float, float],
        killer: Tank | None,
        radius: float = 72,
    ) -> None:
        center_x, center_y = center
        self.add_explosion(center_x, center_y, large=True)
        for target in self.enemies[:]:
            if not target.alive:
                continue
            distance = math.hypot(target.center[0] - center_x, target.center[1] - center_y)
            if distance > radius:
                continue
            killed = target.take_damage(1)
            if killed:
                self.on_enemy_destroyed(target, killer=killer, explosive=False)

    def add_explosion(self, x: float, y: float, large: bool = True) -> None:
        """在指定位置加入爆炸动画。"""
        self.explosions.append(Explosion(x, y, large, rng=self.run_rng))

    def update_hazards(self) -> None:
        remaining: list[Hazard] = []
        for hazard in self.hazards:
            if hazard.update(self):
                remaining.append(hazard)
        self.hazards = remaining

    def damage_player(self, player: PlayerTank | None = None, amount: int = 1, source: str = "") -> None:
        player = player or self.player
        if player is None or not player.alive or self.state != "playing":
            return
        if getattr(self, "code_invincible_active", False):
            self.add_explosion(*player.center, large=False)
            self.message = f"{player.label} 的兑换码无敌抵挡了{source or '攻击'}"
            self.message_timer = 70
            return
        if player.shield_charges > 0:
            player.shield_charges -= 1
            self.add_explosion(*player.center, large=False)
            self.message = f"{player.label} 的能量护盾抵挡了{source or '攻击'}"
            self.message_timer = 80
            return
        player.alive = False
        player.lives -= 1
        player.stats["damage_taken"] += max(1, int(amount))
        self.run_statistics["damage_taken"] += max(1, int(amount))
        self.combo_count = 0
        self.combo_timer = 0
        self.combo_reward_progress = 0
        player.effects.clear()
        self.refresh_tank_speed(player)
        self.add_explosion(*player.center, large=True)
        if player.lives <= 0:
            player.respawn_timer = 0
            self.message = f"{player.label} 已被击毁！"
            self.message_timer = 90
            if self.all_players_exhausted():
                self.finish_game("game_over", "所有玩家的坦克都被击毁了")
        else:
            player.respawn_timer = 75
            self.message = f"{player.label} 被{source or '攻击'}击毁！即将重生"
            self.message_timer = player.respawn_timer
        self.sync_legacy_aliases()

    def all_players_exhausted(self) -> bool:
        return bool(self.players) and all(player.lives <= 0 for player in self.players)

    def respawn_player(self, player: PlayerTank | None = None, silent: bool = False) -> None:
        player = player or self.player
        if player is None or player.lives <= 0:
            return
        player.x, player.y = self.find_player_spawn_position(player)
        player.direction = "up"
        player.alive = True
        player.spawn_protection = 110
        player.fire_cooldown = 0
        player.respawn_timer = 0
        if not silent:
            self.message = f"{player.label} 已重生！"
            self.message_timer = 80

    def destroy_base(self) -> None:
        if self.base is None or not self.base.alive:
            return
        self.base_hits_remaining -= 1
        self.add_explosion(*self.base_center(), large=True)
        if self.base_hits_remaining > 0:
            self.message = "基地进入警报状态：再次受到攻击将被摧毁！"
            self.message_timer = 150
            return
        self.base.alive = False
        self.finish_game("game_over", "基地被摧毁了")

    def base_center(self) -> tuple[float, float]:
        if self.base is None:
            return WINDOW_WIDTH / 2, WINDOW_HEIGHT / 2
        return center_of(self.base.rect)

    def finish_game(self, state: str, reason: str) -> None:
        self.state = state
        self.end_reason = reason
        self.message = reason
        self.message_timer = 0
        self.keys.clear()
        self.key_order.clear()
        self.hover_button = -1
        self.update_records(state)

    def max_lives_for_run(self) -> int:
        """返回本局挑战规则下单个玩家的生命上限。"""
        return 1 if "one_life" in self.selected_challenges else PLAYER_LIVES

    def check_level_progress(self) -> None:
        if self.state != "playing" or not self.wave_active:
            return
        if self.objective_type == "elimination":
            completed = not self.enemies
        elif self.objective_type == "boss":
            # Boss 战目标是击败 Boss，不要求清空护卫等残敌。
            completed = self.current_boss() is None
        else:
            completed = self.objective_complete
        if not completed:
            return
        self.wave_active = False
        self.enemies.clear()
        self.bullets.clear()
        self.hazards.clear()
        cleared_boss_tier = self.boss_tier_for_level(self.level)
        self.run_statistics["levels_cleared"] += 1
        if cleared_boss_tier is not None:
            self.run_statistics["bosses"] += 1
            life_cap = self.max_lives_for_run()
            for player in self.players:
                if player.lives > 0 and player.repair_on_boss > 0:
                    player.lives = min(life_cap, player.lives + player.repair_on_boss)
        if self.game_mode == "story" and self.level >= STORY_MAX_LEVEL:
            self.finish_game("victory", "剧情关卡全部完成")
        else:
            self.begin_upgrade_selection(
                self.level + 1,
                rare_guaranteed=cleared_boss_tier is not None,
            )

    # =========================
    # 道具和效果
    # =========================
    def effects_for(self, entity: Tank | None = None) -> dict[str, int]:
        entity = entity or self.player
        if entity is None:
            return self.active_effects
        if entity is self.player:
            if entity.effects is not self.active_effects:
                entity.effects = self.active_effects
            return self.active_effects
        return entity.effects

    def effect_active(self, kind: str, entity: Tank | None = None) -> bool:
        return self.effects_for(entity).get(kind, 0) > 0

    @staticmethod
    def effect_seconds(remaining_frames: int) -> int:
        return max(1, math.ceil(remaining_frames * FRAME_MS / 1000))

    def refresh_tank_speed(self, entity: Tank) -> None:
        multiplier = SLOW_EFFECT_MULTIPLIER if self.effect_active("slow_tank", entity) else 1.0
        entity.speed = entity.base_speed * entity.move_speed_multiplier * multiplier

    def tank_bullet_speed(self, entity: Tank | None = None) -> float:
        entity = entity or self.player
        speed = PLAYER_BULLET_SPEED if entity is not None and entity.team == "player" else ENEMY_BULLET_SPEED
        if entity is not None:
            speed *= entity.bullet_speed_multiplier
        if entity is not None and self.effect_active("bullet_speed_up", entity):
            speed *= FAST_BULLET_MULTIPLIER
        if entity is not None and self.effect_active("slow_bullets", entity):
            speed *= SLOW_EFFECT_MULTIPLIER
        return max(1.0, speed)

    def player_bullet_speed(self, player: PlayerTank | None = None) -> float:
        return self.tank_bullet_speed(player or self.player)

    def tank_bullet_traits(self, entity: Tank | None = None) -> tuple[int, bool]:
        permanent_bounces = entity.permanent_bounces if entity is not None else 0
        temporary_bounces = MAX_BULLET_BOUNCES if self.effect_active("bounce_bullets", entity) else 0
        bounce = min(8, permanent_bounces + temporary_bounces)
        piercing = self.effect_active("piercing_bullets", entity)
        return bounce, piercing

    def player_bullet_traits(self, player: PlayerTank | None = None) -> tuple[int, bool]:
        return self.tank_bullet_traits(player or self.player)

    def find_powerup_position(self) -> tuple[float, float]:
        min_x = FIELD_PADDING + 12
        max_x = WINDOW_WIDTH - POWERUP_SIZE - FIELD_PADDING - 12
        min_y = FIELD_TOP + 18
        max_y = FIELD_BOTTOM - POWERUP_SIZE - 18
        candidates = [
            (self.run_rng.uniform(min_x, max_x), self.run_rng.uniform(min_y, max_y))
            for _ in range(120)
        ]
        for y in range(int(min_y), int(max_y) + 1, 20):
            for x in range(int(min_x), int(max_x) + 1, 20):
                candidates.append((float(x), float(y)))
        for x, y in candidates:
            rect = (x, y, POWERUP_SIZE, POWERUP_SIZE)
            if any(wall.alive and rectangles_overlap(rect, wall.rect) for wall in self.walls):
                continue
            if self.base is not None and self.base.alive and rectangles_overlap(rect, self.base.rect):
                continue
            if any(entity.alive and rectangles_overlap(rect, entity.rect) for entity in [*self.players, *self.enemies]):
                continue
            if any(rectangles_overlap(rect, item.rect) for item in self.powerups):
                continue
            return x, y
        return max(min_x, min(WINDOW_WIDTH / 2 - POWERUP_SIZE / 2, max_x)), max(min_y, FIELD_TOP + 40)

    def spawn_powerup(self) -> None:
        # 自然掉落只生成盲盒，不提前暴露其中的 Buff/Debuff。
        kind = self.run_rng.choice(POWERUP_TYPES)
        x, y = self.find_powerup_position()
        self.powerups.append(PowerUp(kind, x, y))
        self.message = f"神秘盲盒已出现（拾取后随机获得效果，{POWERUP_LIFETIME_SECONDS} 秒后消失）"
        self.message_timer = 110

    def choose_blind_box_effect(self, collector: Tank) -> str:
        if "chaotic_boxes" in self.selected_challenges:
            return self.run_rng.choice(BLIND_BOX_EFFECT_TYPES)
        if not isinstance(collector, PlayerTank) or collector.blind_box_luck <= 0:
            return self.run_rng.choice(BLIND_BOX_EFFECT_TYPES)
        weights = [
            1.0 + collector.blind_box_luck
            if POWERUP_INFO[kind]["category"] == "buff"
            else 1.0
            for kind in BLIND_BOX_EFFECT_TYPES
        ]
        return self.run_rng.choices(BLIND_BOX_EFFECT_TYPES, weights=weights, k=1)[0]

    def activate_powerup(self, powerup: PowerUp, collector: Tank | None = None) -> None:
        collector = collector or self.player
        if collector is None:
            return

        if powerup.kind == BLIND_BOX_KIND:
            # 盲盒第一次被拾取时才决定效果；记录结果便于状态追踪和测试。
            effect_kind = powerup.assigned_effect or self.choose_blind_box_effect(collector)
            powerup.assigned_effect = effect_kind
        else:
            # 兼容旧式直接构造具体效果道具的调用。
            effect_kind = powerup.kind

        info = POWERUP_INFO[effect_kind]
        duration_multiplier = (
            collector.effect_duration_multiplier
            if isinstance(collector, PlayerTank)
            else 1.0
        )
        duration = max(1, round(EFFECT_DURATION_FRAMES * duration_multiplier))
        self.effects_for(collector)[effect_kind] = duration
        if isinstance(collector, PlayerTank):
            collector.stats["blind_boxes"] += 1
            self.run_statistics["blind_boxes"] += 1
            if self.objective_type == "blind_box_race":
                self.objective_progress += 1
        elif self.objective_type == "blind_box_race":
            self.enemy_boxes_taken += 1
        self.refresh_tank_speed(collector)
        self.add_explosion(*powerup.center, large=False)
        who = collector.label
        prefix = "获得强化" if info["category"] == "buff" else "受到干扰"
        seconds = self.effect_seconds(duration)
        self.message = f"{who}开启盲盒：{prefix}·{info['name']}（{seconds} 秒）"
        self.message_timer = 120

    def update_powerups(self) -> None:
        if self.state != "playing":
            return
        self.drop_timer -= 1
        if self.drop_timer <= 0:
            self.spawn_powerup()
            self.drop_timer = DROP_INTERVAL_FRAMES

        for powerup in self.powerups[:]:
            powerup.remaining_frames -= 1
            if powerup.remaining_frames <= 0:
                self.powerups.remove(powerup)
                self.add_explosion(*powerup.center, large=False)
                continue
            collector = next(
                (
                    entity
                    for entity in [*self.players, *self.enemies]
                    if entity.alive and rectangles_overlap(entity.rect, powerup.rect)
                ),
                None,
            )
            if collector is not None:
                self.activate_powerup(powerup, collector)
                self.powerups.remove(powerup)

    def update_effects(self) -> None:
        expired_messages: list[str] = []
        for entity in [*self.players, *self.enemies]:
            effects = self.effects_for(entity)
            expired: list[str] = []
            for kind, remaining in list(effects.items()):
                remaining -= 1
                if remaining <= 0:
                    del effects[kind]
                    expired.append(kind)
                else:
                    effects[kind] = remaining
            self.refresh_tank_speed(entity)
            if expired:
                names = "、".join(POWERUP_INFO[kind]["name"] for kind in expired)
                expired_messages.append(f"{entity.label}的{names}结束")
                if (
                    isinstance(entity, PlayerTank)
                    and entity.risk_investment
                    and any(POWERUP_INFO[kind]["category"] == "debuff" for kind in expired)
                ):
                    x, y = self.find_powerup_position()
                    self.powerups.append(PowerUp(BLIND_BOX_KIND, x, y))
        if expired_messages:
            self.message = "；".join(expired_messages)
            self.message_timer = 80

    def sync_legacy_aliases(self) -> None:
        if self.player is not None:
            self.active_effects = self.player.effects
            self.lives = self.player.lives

    # =========================
    # 绘制
    # =========================
    def draw_background(self) -> None:
        self.canvas.create_rectangle(0, 0, WINDOW_WIDTH, HUD_HEIGHT, fill=HUD_BACKGROUND, outline="")
        self.canvas.create_rectangle(0, FIELD_TOP, WINDOW_WIDTH, WINDOW_HEIGHT, fill=FIELD_BACKGROUND, outline="")
        for x in range(0, WINDOW_WIDTH + 1, 40):
            self.canvas.create_line(x, FIELD_TOP, x, WINDOW_HEIGHT, fill=FIELD_GRID)
        for y in range(FIELD_TOP, WINDOW_HEIGHT + 1, 40):
            self.canvas.create_line(0, y, WINDOW_WIDTH, y, fill=FIELD_GRID)
        self.canvas.create_rectangle(
            FIELD_PADDING,
            FIELD_TOP,
            WINDOW_WIDTH - FIELD_PADDING,
            FIELD_BOTTOM,
            outline=FIELD_BORDER,
            width=3,
        )

    def draw_small_view_overlay(self) -> None:
        visible_players = [
            player for player in self.players
            if player.alive and self.effect_active("small_view", player)
        ]
        if not visible_players:
            return
        strip_height = 8
        y = FIELD_TOP
        while y < FIELD_BOTTOM:
            next_y = min(y + strip_height, FIELD_BOTTOM)
            middle_y = (y + next_y) / 2
            intervals: list[tuple[float, float]] = []
            for player in visible_players:
                center_x, center_y = player.center
                dy = middle_y - center_y
                if abs(dy) < SMALL_VIEW_RADIUS:
                    half = math.sqrt(max(0.0, SMALL_VIEW_RADIUS**2 - dy**2))
                    intervals.append((max(0.0, center_x - half), min(float(WINDOW_WIDTH), center_x + half)))
            intervals.sort()
            merged: list[list[float]] = []
            for left, right in intervals:
                if not merged or left > merged[-1][1]:
                    merged.append([left, right])
                else:
                    merged[-1][1] = max(merged[-1][1], right)
            cursor = 0.0
            for left, right in merged:
                if left > cursor:
                    self.canvas.create_rectangle(cursor, y, left, next_y, fill=FOG_COLOR, outline="")
                cursor = max(cursor, right)
            if cursor < WINDOW_WIDTH:
                self.canvas.create_rectangle(cursor, y, WINDOW_WIDTH, next_y, fill=FOG_COLOR, outline="")
            y = next_y
        for player in visible_players:
            center_x, center_y = player.center
            self.canvas.create_oval(
                center_x - SMALL_VIEW_RADIUS,
                center_y - SMALL_VIEW_RADIUS,
                center_x + SMALL_VIEW_RADIUS,
                center_y + SMALL_VIEW_RADIUS,
                outline="#6f5ca8",
                width=1,
            )

    def format_effects(self, entity: Tank) -> str:
        effects = self.effects_for(entity)
        if not effects:
            return "无"
        parts: list[str] = []
        for kind, remaining in effects.items():
            info = POWERUP_INFO[kind]
            marker = "+" if info["category"] == "buff" else "-"
            parts.append(f"{marker}{info['short']} {self.effect_seconds(remaining)}s")
        return "、".join(parts)

    def format_upgrades(self, player: PlayerTank) -> str:
        if not player.run_upgrades:
            return "无"
        parts = [
            f"{UPGRADE_DEFINITIONS[key].short}×{level}"
            for key, level in player.run_upgrades.items()
            if key in UPGRADE_DEFINITIONS
        ]
        if len(parts) > 4:
            return "、".join(parts[:4]) + f" 等{len(parts)}项"
        return "、".join(parts)

    def current_boss(self) -> Boss | None:
        return next((enemy for enemy in self.enemies if isinstance(enemy, Boss) and enemy.alive), None)

    def draw_boss_hud(self) -> None:
        boss = self.current_boss()
        if boss is None:
            return
        left, top, width, height = 280, 108, 340, 10
        ratio = max(0.0, min(1.0, boss.health / boss.max_health))
        self.canvas.create_text(
            left - 10,
            top + 5,
            anchor="e",
            text=f"{boss.boss_name} · 阶段 {boss.phase}",
            fill="#ffd166",
            font=("Microsoft YaHei", 9, "bold"),
        )
        self.canvas.create_rectangle(left, top, left + width, top + height, fill=BOSS_HEALTH_BACKGROUND, outline="#a64b68")
        self.canvas.create_rectangle(left + 2, top + 2, left + 2 + (width - 4) * ratio, top + height - 2, fill=BOSS_HEALTH_FILL, outline="")
        self.canvas.create_text(
            left + width + 10,
            top + 5,
            anchor="w",
            text=f"{boss.health}/{boss.max_health}",
            fill=TEXT_COLOR,
            font=("Arial", 8, "bold"),
        )

    def draw_hud(self) -> None:
        self.canvas.create_line(0, HUD_HEIGHT, WINDOW_WIDTH, HUD_HEIGHT, fill="#385464", width=2)
        mode_text = "剧情模式" if self.game_mode == "story" else f"无尽模式 ×{self.challenge_score_multiplier:.2f}"
        status_text = {
            "playing": "战斗中",
            "paused": "已暂停",
            "level_intro": "关卡准备",
            "countdown": "倒计时",
            "victory": "胜利",
            "game_over": "游戏结束",
        }.get(self.state, "")
        status_color = PLAYER_COLOR if self.state in ("playing", "victory") else PLAYER_BULLET_COLOR if self.state in ("paused", "level_intro", "countdown") else ENEMY_COLOR
        level_text = f"第 {self.level} 关" if self.game_mode == "endless" else f"第 {self.level}/{STORY_MAX_LEVEL} 关"
        self.canvas.create_text(16, 18, anchor="w", text="坦克大战", fill=TEXT_COLOR, font=("Microsoft YaHei", 18, "bold"))
        self.canvas.create_text(155, 18, anchor="w", text=f"得分：{self.score}  连击：{self.combo_count}", fill=PLAYER_BULLET_COLOR, font=("Microsoft YaHei", 10, "bold"))
        self.canvas.create_text(260, 18, anchor="w", text=level_text, fill=BASE_COLOR, font=("Microsoft YaHei", 11, "bold"))
        self.canvas.create_text(390, 18, anchor="w", text=mode_text, fill="#c6dcff", font=("Microsoft YaHei", 11, "bold"))
        self.canvas.create_text(505, 18, anchor="w", text=self.current_map_name, fill="#d6c6ff", font=("Microsoft YaHei", 9, "bold"))
        self.canvas.create_text(WINDOW_WIDTH - 18, 18, anchor="e", text=status_text, fill=status_color, font=("Microsoft YaHei", 11, "bold"))
        self.canvas.create_text(525, 92, anchor="center", text=self.objective_status_text(), width=310, fill="#ffe082", font=("Microsoft YaHei", 8, "bold"))
        if self.players:
            p1 = self.players[0]
            self.canvas.create_text(16, 46, anchor="w", text=f"P1 生命：{p1.lives}  临时：{self.format_effects(p1)}  构筑：{self.format_upgrades(p1)}", fill=PLAYER_COLOR, font=("Microsoft YaHei", 8))
            if len(self.players) > 1:
                p2 = self.players[1]
                self.canvas.create_text(16, 69, anchor="w", text=f"P2 生命：{p2.lives}  临时：{self.format_effects(p2)}  构筑：{self.format_upgrades(p2)}", fill=PLAYER_TWO_COLOR, font=("Microsoft YaHei", 8))
                controls = "P1：WASD+空格   P2：方向键+Enter"
            else:
                self.canvas.create_text(16, 69, anchor="w", text=f"本局构筑：{self.format_upgrades(p1)}", fill="#c5e1ff", font=("Microsoft YaHei", 8))
                controls = "操作：WASD/方向键移动   空格射击"
            self.canvas.create_text(16, 96, anchor="w", text=controls, fill=MUTED_TEXT, font=("Microsoft YaHei", 9))
        drop_seconds = self.effect_seconds(self.drop_timer) if self.drop_timer > 0 else 0
        self.canvas.create_text(WINDOW_WIDTH - 18, 46, anchor="e", text=f"下次盲盒：{drop_seconds} 秒", fill="#9ce7ff", font=("Microsoft YaHei", 9, "bold"))
        self.canvas.create_text(WINDOW_WIDTH - 18, 69, anchor="e", text=f"倍率 ×{self.combo_multiplier():.2f}  奖励盲盒 {self.combo_reward_progress}/{COMBO_REWARD_KILLS}", fill=MUTED_TEXT, font=("Microsoft YaHei", 8))
        self.canvas.create_text(WINDOW_WIDTH - 18, 96, anchor="e", text="P暂停  R重开  M菜单  Esc退出", fill=MUTED_TEXT, font=("Microsoft YaHei", 9))
        self.draw_boss_hud()

    def draw_message(self) -> None:
        if not self.message or self.message_timer <= 0:
            return
        width = 430
        left = (WINDOW_WIDTH - width) / 2
        top = FIELD_TOP + 12
        self.canvas.create_rectangle(left, top, left + width, top + 36, fill="#102c36", outline=MENU_ACCENT, width=2)
        self.canvas.create_text(WINDOW_WIDTH / 2, top + 18, text=self.message, fill=TEXT_COLOR, font=("Microsoft YaHei", 11, "bold"))

    def draw_battle_scene(self) -> None:
        self.draw_background()
        for wall in self.walls:
            wall.draw(self.canvas)
        if self.base is not None:
            self.base.draw(self.canvas)
        for powerup in self.powerups:
            powerup.draw(self.canvas, self.frame)
        for hazard in self.hazards:
            hazard.draw(self.canvas)
        for bullet in self.bullets:
            bullet.draw(self.canvas)
        for enemy in self.enemies:
            enemy.draw(self.canvas)
        for player in self.players:
            player.draw(self.canvas)
        for explosion in self.explosions:
            explosion.draw(self.canvas)
        self.draw_small_view_overlay()
        self.draw_hud()

    def draw_upgrade_select(self) -> None:
        self.draw_menu_background()
        player = self.current_upgrade_player()
        if player is None:
            return
        rare_text = " · Boss 稀有奖励" if self.pending_rare_upgrade else ""
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            72,
            text=f"战术整备{rare_text}",
            fill=TEXT_COLOR,
            font=("Microsoft YaHei", 30, "bold"),
        )
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            116,
            text=f"{player.label} 请选择一项本局永久升级（数字键 1 / 2 / 3）",
            fill=PLAYER_COLOR if player.player_id == 1 else PLAYER_TWO_COLOR,
            font=("Microsoft YaHei", 13, "bold"),
        )
        card_width = 250
        card_height = 360
        gap = 25
        start_x = (WINDOW_WIDTH - (card_width * 3 + gap * 2)) / 2
        for index, definition in enumerate(self.upgrade_choices):
            x = start_x + index * (card_width + gap)
            y = 165
            hovered = len(self.menu_buttons) == self.hover_button
            fill = "#274a5b" if hovered else MENU_PANEL
            outline = "#ffe082" if definition.rarity == "rare" else MENU_ACCENT
            self.canvas.create_rectangle(
                x,
                y,
                x + card_width,
                y + card_height,
                fill=fill,
                outline=outline,
                width=3 if hovered else 2,
            )
            self.canvas.create_text(
                x + 18,
                y + 25,
                anchor="w",
                text=f"{index + 1}  {definition.name}",
                fill="#ffd166" if definition.rarity == "rare" else TEXT_COLOR,
                font=("Microsoft YaHei", 17, "bold"),
            )
            self.canvas.create_text(
                x + card_width / 2,
                y + 78,
                text=f"{definition.category} · {'稀有' if definition.rarity == 'rare' else '普通'}",
                fill=MENU_ACCENT,
                font=("Microsoft YaHei", 11, "bold"),
            )
            current_level = player.upgrade_level(definition.key)
            self.canvas.create_text(
                x + card_width / 2,
                y + 128,
                text=f"当前 {current_level} 层  →  {current_level + 1}/{definition.max_level} 层",
                fill=MUTED_TEXT,
                font=("Microsoft YaHei", 10),
            )
            self.canvas.create_text(
                x + card_width / 2,
                y + 215,
                text=definition.description,
                width=card_width - 38,
                fill=TEXT_COLOR,
                font=("Microsoft YaHei", 12, "bold"),
            )
            self.canvas.create_text(
                x + card_width / 2,
                y + card_height - 42,
                text="点击选择",
                fill="#b8f2e6",
                font=("Microsoft YaHei", 10),
            )
            self.menu_buttons.append(
                {
                    "x1": x,
                    "y1": y,
                    "x2": x + card_width,
                    "y2": y + card_height,
                    "action": f"choose_upgrade_{index}",
                }
            )
        self.canvas.create_text(
            WINDOW_WIDTH / 2,
            575,
            text=f"本局种子：{self.run_seed}    选择后进入第 {self.pending_next_level} 关    M：返回菜单",
            fill=MUTED_TEXT,
            font=("Microsoft YaHei", 10),
        )

    def draw_pre_game_overlay(self) -> None:
        self.canvas.create_rectangle(0, FIELD_TOP, WINDOW_WIDTH, FIELD_BOTTOM, fill="#08131d", outline="")
        center_x = WINDOW_WIDTH / 2
        center_y = (FIELD_TOP + FIELD_BOTTOM) / 2
        player_text = "单人作战" if self.player_mode == "single" else "双人协作"
        if self.state == "level_intro":
            boss = self.boss_tier_for_level(self.level)
            extra = ""
            if boss == "mini":
                extra = " · 小 Boss 来袭"
            elif boss == "mega":
                extra = " · 大 Boss 降临"
            self.canvas.create_text(center_x, center_y - 45, text=f"第 {self.level} 关{extra}", fill=MENU_ACCENT, font=("Microsoft YaHei", 40, "bold"))
            self.canvas.create_text(center_x, center_y + 8, text=f"{player_text} · {'剧情模式' if self.game_mode == 'story' else '无尽模式'} · {self.current_map_name}", fill=TEXT_COLOR, font=("Microsoft YaHei", 15, "bold"))
            self.canvas.create_text(center_x, center_y + 45, text=self.objective_status_text(), fill="#ffe082", font=("Microsoft YaHei", 12, "bold"))
            self.canvas.create_text(center_x, center_y + 75, text="战场部署中……", fill=MUTED_TEXT, font=("Microsoft YaHei", 10))
        else:
            self.canvas.create_text(center_x, center_y - 35, text=str(self.countdown_number()), fill=PLAYER_BULLET_COLOR, font=("Arial", 86, "bold"))
            self.canvas.create_text(center_x, center_y + 55, text="准备战斗！", fill=TEXT_COLOR, font=("Microsoft YaHei", 18, "bold"))

    def draw_end_overlay(self) -> None:
        self.canvas.create_rectangle(0, FIELD_TOP, WINDOW_WIDTH, FIELD_BOTTOM, fill="#0a131d", outline="")
        center_x = WINDOW_WIDTH / 2
        center_y = (FIELD_TOP + FIELD_BOTTOM) / 2
        if self.state == "paused":
            title, detail, color = "游戏暂停", "按 P 继续战斗，按 M 返回主菜单", PLAYER_BULLET_COLOR
        elif self.state == "victory":
            title, detail, color = "胜利！", f"{self.end_reason}    最终得分：{self.score}", PLAYER_COLOR
        else:
            title, detail, color = "游戏结束", f"{self.end_reason}    最终得分：{self.score}", ENEMY_COLOR
        self.canvas.create_text(center_x, center_y - 70, text=title, fill=color, font=("Microsoft YaHei", 34, "bold"))
        self.canvas.create_text(center_x, center_y - 25, text=detail, fill=TEXT_COLOR, font=("Microsoft YaHei", 13))
        if self.state in ("victory", "game_over"):
            self.canvas.create_text(
                center_x,
                center_y + 10,
                text=f"最高连击：{self.best_combo}  已通关：{self.run_statistics['levels_cleared']}  精英击破：{self.run_statistics['elite_kills']}  盲盒：{self.run_statistics['blind_boxes']}",
                fill="#ffe082",
                font=("Microsoft YaHei", 10, "bold"),
            )
            player_stats = "    ".join(
                f"{player.label} 击杀{player.stats['kills']} 精英{player.stats['elite_kills']} 掩护{player.stats['cover_kills']} Boss伤害{player.stats['boss_damage']}"
                for player in self.players
            )
            self.canvas.create_text(
                center_x,
                center_y + 38,
                text=player_stats,
                fill=MUTED_TEXT,
                font=("Microsoft YaHei", 9),
            )
            challenge_names = "、".join(
                CHALLENGE_INFO[key][0] for key in sorted(self.selected_challenges)
            ) or "无契约"
            self.canvas.create_text(
                center_x,
                center_y + 63,
                text=f"种子 {self.run_seed} · {challenge_names} · 历史最高分 {self.records['high_score']} · 无尽最高 {self.records['highest_endless_level']} 关",
                fill="#b8f2e6",
                font=("Microsoft YaHei", 9, "bold"),
            )
            self.draw_button(260, center_y + 98, 170, 52, "再来一局", "restart")
            self.draw_button(470, center_y + 98, 170, 52, "返回菜单", "main_menu")

    def render(self) -> None:
        self.canvas.delete("all")
        self.menu_buttons = []
        if self.state == "main_menu":
            self.draw_main_menu()
        elif self.state == "player_mode_select":
            self.draw_player_mode_select()
        elif self.state == "game_mode_select":
            self.draw_game_mode_select()
        elif self.state == "challenge_select":
            self.draw_challenge_select()
        elif self.state == "upgrade_select":
            self.draw_upgrade_select()
        else:
            self.draw_battle_scene()
            if self.state in ("level_intro", "countdown"):
                self.draw_pre_game_overlay()
            elif self.state == "playing":
                self.draw_message()
            else:
                self.draw_end_overlay()

    def run(self) -> None:
        self.root.mainloop()


def main() -> None:
    game = TankBattleGame()
    game.run()


if __name__ == "__main__":
    main()
