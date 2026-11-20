using System;
using System.Collections.Generic;
using UnityEngine;
using Modding;

namespace JoJoStandMod
{
    // Stands para o Cavaleiro. O Stand ativo depende dos amuletos equipados:
    //   Sombra do Cavaleiro : padrao.                            Especial = magia
    //   Star Platinum       : Heavy Blow (15) + Quick Slash (32). Especial = time stop / Star Finger / soco no chao
    //   Gold Experience     : Lifeblood Heart (8) + Hiveblood (29). Especial = cura
    //   Purple Haze         : Fury of the Fallen (6) + Spore Shroom (17). Especial = virus (nuvens de veneno)
    // Controles:
    //   Toque rapido na magia rapida : Stand aparece/some atras do Cavaleiro
    //   Botao do ferrao (toques)     : murro; clicando varias vezes faz combo e termina com soco forte
    //   Segurar o ferrao             : barragem
    //   Segurar a cura               : habilidade especial (neutro / mirando para CIMA / mirando para BAIXO)
    public class JoJoStandMod : Mod
    {
        public const string ModName = "JoJo Stands";
        public const string ModVersion = "1.5.0";

        public JoJoStandMod() : base(ModName) { }

        // ---- combinacoes de amuletos (numero do amuleto no jogo) ----
        const int StarCharmA = 15, StarCharmB = 32;
        const int GoldCharmA = 8, GoldCharmB = 29;
        const int HazeCharmA = 6, HazeCharmB = 17;

        // ---- ajustes gerais ----
        const float TapTime = 0.30f;
        const float HoldTime = 0.45f;
        const float ComboWindow = 0.60f;
        const int ComboLength = 4;
        const int PunchDamage = 3;
        const int HeavyDamage = 12;
        const int BarrageDamage = 2;
        const float PunchReach = 4.5f;
        const float PunchHeight = 2.5f;
        const float BarrageTime = 2.5f;
        const float BarrageInterval = 0.06f;
        const float BackOffset = -1.3f;
        const int SpecialSoulCost = 33;

        // Star Platinum
        const float WorldDuration = 5f;
        const float WorldRange = 40f;
        const int FingerDamage = 15;
        const float FingerLength = 14f;
        const float FingerHalfWidth = 1.3f;
        const float SlamRadius = 7f;
        const int SlamDamage = 6;
        const float JuggleHeight = 5f;
        const float JuggleRiseSpeed = 22f;
        const float JuggleTime = 2.0f;
        const float JuggleHitInterval = 0.08f;
        const int JuggleHitDamage = 2;
        const int JuggleFinalDamage = 12;

        // Sombra
        const int OrbDamage = 8;
        const float OrbSpeed = 18f;
        const float OrbLife = 1.2f;
        const float BlastRadius = 6f;
        const int BlastDamage = 15;

        // Gold Experience
        const int GoldHeal = 2;
        const int GoldHealUp = 3;

        // Purple Haze
        const float HazeBarrageInterval = 0.045f; // passivo: socos mais rapidos (descontrolado)
        const int CloudDamage = 2;
        const float CloudTick = 0.4f;
        const float GasRadius = 5f;
        const float GasLife = 5f;
        const float CapsuleSpeed = 14f;
        const float CapsuleGravity = 22f;
        const float CapsuleCloudRadius = 4f;
        const float CapsuleCloudLife = 4f;
        const float OutbreakRadius = 8f;
        const float OutbreakLife = 6f;
        const int OutbreakDamage = 3;

        enum Kind { Shadow = 0, Star = 1, Gold = 2, Haze = 3 }

        class Frozen
        {
            public HealthManager Hm;
            public PlayMakerFSM[] Fsms;
            public bool[] FsmWasEnabled;
            public Rigidbody2D Body;
            public bool WasKinematic;
            public bool Revived;
        }

        class Juggled
        {
            public HealthManager Hm;
            public Rigidbody2D Body;
            public bool WasKinematic;
            public float TargetY;
        }

        class Orb
        {
            public GameObject Go;
            public Vector2 Vel;
            public float Life;
        }

        class Cloud
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public Vector2 Center;
            public float Radius, Life, Total, Tick;
            public int Dmg;
        }

        class Capsule
        {
            public GameObject Go;
            public Vector2 Vel;
            public float Life;
        }

        class Fx
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public float Life, Total;
            public Vector3 S0, S1;
            public Color C;
        }

        static readonly Sprite[] _sprites = new Sprite[4];
        static Sprite _orbSprite;
        static Sprite _solidSprite;

        class StandSet
        {
            public Sprite[] Idle, Light, Heavy, Barrage, Finger;
        }
        static readonly StandSet[] _sets = new StandSet[4];
        StandSet _set;
        int _punchStyle;   // 0 = leve, 1 = forte, 2 = barragem

        Kind _kind = Kind.Shadow;
        float _kindTimer;

        GameObject _standGo;
        SpriteRenderer _sr;
        bool _active;

        float _quickHold, _attackHold, _focusHold;
        bool _barrageUsed, _focusUsed;
        float _barrageLeft, _barrageTick;
        int _combo;
        float _comboTimer;

        bool _punching, _punchHit;
        float _punchT, _punchDur, _punchExtend, _punchReach, _punchScale;
        int _punchDmg;
        float _poseUp;

        readonly List<Frozen> _frozen = new List<Frozen>();
        float _worldLeft;

        readonly List<Juggled> _juggled = new List<Juggled>();
        float _juggleLeft, _juggleTick;

        readonly List<Orb> _orbs = new List<Orb>();
        readonly List<Fx> _fx = new List<Fx>();
        readonly List<Cloud> _clouds = new List<Cloud>();
        readonly List<Capsule> _capsules = new List<Capsule>();

        HealthManager[] _enemies;
        float _lastScan = -10f;

        public override string GetVersion()
        {
            return ModVersion;
        }

        public override void Initialize()
        {
            Log(ModName + " v" + ModVersion + " carregado");
            ModHooks.HeroUpdateHook += OnHeroUpdate;
        }

        void OnHeroUpdate()
        {
            try { Tick(); }
            catch (Exception e) { LogError("Erro: " + e); }
        }

        static bool Eq(PlayerData pd, int charm)
        {
            return pd.GetBool("equippedCharm_" + charm);
        }

        Kind ChooseKind()
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) return Kind.Shadow;
            if (Eq(pd, StarCharmA) && Eq(pd, StarCharmB)) return Kind.Star;
            if (Eq(pd, GoldCharmA) && Eq(pd, GoldCharmB)) return Kind.Gold;
            if (Eq(pd, HazeCharmA) && Eq(pd, HazeCharmB)) return Kind.Haze;
            return Kind.Shadow;
        }

        void Tick()
        {
            HeroController hero = HeroController.instance;
            InputHandler ih = InputHandler.Instance;
            if (hero == null || ih == null) return;
            HeroActions a = ih.inputActions;
            float dt = Time.deltaTime;

            // --- Stand conforme os amuletos equipados
            _kindTimer -= dt;
            if (_kindTimer <= 0f)
            {
                _kindTimer = 0.5f;
                Kind k = ChooseKind();
                if (k != _kind)
                {
                    _kind = k;
                    Log("Stand atual: " + k);
                    DestroyStand();
                }
            }

            // --- magia rapida: toque rapido liga/desliga o Stand
            if (a.quickCast.IsPressed) _quickHold += dt;
            if (a.quickCast.WasReleased)
            {
                if (_quickHold < TapTime) ToggleStand();
                _quickHold = 0f;
            }

            // --- ferrao: toque = murro/combo, segurar = barragem
            if (a.attack.WasPressed)
            {
                _attackHold = 0f;
                if (_active) ComboPunch();
            }
            if (a.attack.IsPressed)
            {
                _attackHold += dt;
                if (_active && !_barrageUsed && _attackHold >= HoldTime)
                {
                    _barrageUsed = true;
                    _barrageLeft = BarrageTime;
                }
            }
            else
            {
                _attackHold = 0f;
                _barrageUsed = false;
                _barrageLeft = 0f;
            }

            // --- cura: segurar = habilidade especial (direcao: cima / baixo / neutro)
            if (a.focus.IsPressed)
            {
                _focusHold += dt;
                if (_active && !_focusUsed && _focusHold >= HoldTime)
                {
                    _focusUsed = true;
                    UseSpecial(hero, a.up.IsPressed, a.down.IsPressed);
                }
            }
            else
            {
                _focusHold = 0f;
                _focusUsed = false;
            }

            // --- timers
            if (_comboTimer > 0f)
            {
                _comboTimer -= dt;
                if (_comboTimer <= 0f) _combo = 0;
            }
            if (_barrageLeft > 0f)
            {
                _barrageLeft -= dt;
                _barrageTick -= dt;
                if (_barrageTick <= 0f)
                {
                    _barrageTick = _kind == Kind.Haze ? HazeBarrageInterval : BarrageInterval;
                    StartPunch(0.06f, 2.0f, BarrageDamage, PunchReach, 1f, 2);
                }
            }
            if (_worldLeft > 0f)
            {
                _worldLeft -= dt;
                ReviveDead();
                if (_worldLeft <= 0f) EndTheWorld();
            }

            UpdateJuggle(dt);
            UpdateOrbs(dt);
            UpdateCapsules(dt);
            UpdateClouds(dt);
            UpdateFx(dt);

            if (_active)
            {
                if (_standGo == null) CreateStand();
                UpdateStand(hero, dt);
            }
        }

        // ---------------- Stand ----------------

        void ToggleStand()
        {
            _active = !_active;
            if (!_active)
            {
                DestroyStand();
                _barrageLeft = 0f;
                _combo = 0;
            }
        }

        void DestroyStand()
        {
            if (_standGo != null) UnityEngine.Object.Destroy(_standGo);
            _standGo = null;
            _sr = null;
            _punching = false;
        }

        void CreateStand()
        {
            _set = GetSet(_kind);
            _standGo = new GameObject("JoJoStand");
            _sr = _standGo.AddComponent<SpriteRenderer>();
            _sr.sprite = _set.Idle[0];
            _sr.sortingOrder = 5;
        }

        // Conjunto de sprites de cada Stand (Star Platinum usa sprites reais; os outros, desenhados por codigo)
        StandSet GetSet(Kind kind)
        {
            int i = (int)kind;
            if (_sets[i] != null) return _sets[i];
            StandSet set = new StandSet();
            if (kind == Kind.Star)
            {
                set.Idle = LoadFrames("sp_5_0", "sp_5_1");
                set.Light = LoadFrames("sp_205_10", "sp_205_12", "sp_205_13");
                set.Heavy = LoadFrames("sp_205_53", "sp_205_54", "sp_205_55");
                set.Barrage = LoadFrames("sp_205_45", "sp_205_46", "sp_205_47", "sp_205_48");
                set.Finger = LoadFrames("sp_3461_0", "sp_3461_1", "sp_3461_2", "sp_3461_3", "sp_3461_4", "sp_3461_5", "sp_3461_6");
            }
            if (set.Idle == null || set.Light == null || set.Heavy == null || set.Barrage == null || set.Finger == null)
            {
                if (_sprites[i] == null) _sprites[i] = BuildSprite(kind);
                Sprite[] one = new Sprite[] { _sprites[i] };
                if (set.Idle == null) set.Idle = one;
                if (set.Light == null) set.Light = one;
                if (set.Heavy == null) set.Heavy = one;
                if (set.Barrage == null) set.Barrage = one;
                if (set.Finger == null) set.Finger = one;
            }
            _sets[i] = set;
            return set;
        }

        Sprite[] LoadFrames(params string[] names)
        {
            Sprite[] r = new Sprite[names.Length];
            for (int k = 0; k < names.Length; k++)
            {
                r[k] = LoadSprite(names[k]);
                if (r[k] == null) return null;
            }
            return r;
        }

        Sprite LoadSprite(string name)
        {
            try
            {
                int idx = Array.IndexOf(StandSprites.Names, name);
                if (idx < 0) return null;
                byte[] bytes = Convert.FromBase64String(StandSprites.Png[idx]);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                if (!tex.LoadImage(bytes)) return null;
                tex.hideFlags = HideFlags.HideAndDontSave;
                Sprite sp = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height),
                    new Vector2(StandSprites.PivotX[idx], StandSprites.PivotY[idx]), 26f);
                sp.hideFlags = HideFlags.HideAndDontSave;
                return sp;
            }
            catch (Exception e)
            {
                LogError("Falha ao carregar sprite " + name + ": " + e.Message);
                return null;
            }
        }

        bool JuggleCentroid(out Vector3 c)
        {
            c = Vector3.zero;
            int n = 0;
            for (int i = 0; i < _juggled.Count; i++)
            {
                HealthManager hm = _juggled[i].Hm;
                if (hm == null || hm.hp <= 0) continue;
                c += hm.transform.position;
                n++;
            }
            if (n == 0) return false;
            c /= n;
            return true;
        }

        void UpdateStand(HeroController hero, float dt)
        {
            bool right = hero.cState.facingRight;
            float dir = right ? 1f : -1f;
            Vector3 hp = hero.transform.position;
            Vector3 anchor = hp;
            float bob = Mathf.Sin(Time.time * 3f) * 0.1f;
            float forward = BackOffset;
            float scale = 1f;
            float rot = 0f;
            Sprite[] frames = _set.Idle;
            float fp = (Time.time * 2f) % 1f;

            Vector3 cen;
            if (_juggleLeft > 0f && JuggleCentroid(out cen))
                anchor = new Vector3(cen.x, cen.y - 0.8f, hp.z);

            if (_poseUp > 0f)
            {
                _poseUp -= dt;
                rot = right ? 90f : -90f;
                anchor = new Vector3(hp.x, hp.y + 1.4f, hp.z);
                forward = 0f;
                frames = _set.Finger;
                fp = 1f - _poseUp / 0.3f;
            }
            else if (_punching)
            {
                _punchT += dt;
                float p = Mathf.Clamp01(_punchT / _punchDur);
                float f = Mathf.Sin(p * Mathf.PI);
                forward = Mathf.Lerp(BackOffset, _punchExtend, f);
                scale = Mathf.Lerp(1f, _punchScale, f);
                frames = _punchStyle == 1 ? _set.Heavy : (_punchStyle == 2 ? _set.Barrage : _set.Light);
                fp = _punchStyle == 2 ? (Time.time * 20f) % 1f : p;
                if (!_punchHit && p >= 0.5f)
                {
                    _punchHit = true;
                    if (_punchDmg > 0) Hit(hp, dir, _punchDmg, _punchReach);
                }
                if (p >= 1f) _punching = false;
            }

            int fi = Mathf.Clamp((int)(fp * frames.Length), 0, frames.Length - 1);
            _sr.sprite = frames[fi];
            _standGo.transform.position = new Vector3(anchor.x + dir * forward, anchor.y + 0.3f + bob, hp.z + 0.02f);
            _standGo.transform.localScale = new Vector3(scale, scale, 1f);
            _standGo.transform.rotation = Quaternion.Euler(0f, 0f, rot);
            _sr.flipX = !right;
            _sr.color = _worldLeft > 0f ? new Color(0.6f, 0.8f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.9f);
        }

        void ComboPunch()
        {
            _comboTimer = ComboWindow;
            _combo++;
            if (_combo > ComboLength)
            {
                _combo = 0;
                StartPunch(0.28f, 3.2f, HeavyDamage, PunchReach + 1f, 1.2f, 1);
            }
            else
            {
                StartPunch(0.12f, 2.0f, PunchDamage, PunchReach, 1f);
            }
        }

        void StartPunch(float duration, float extend, int dmg, float reach, float scale, int style = 0)
        {
            _punchStyle = style;
            _punching = true;
            _punchHit = false;
            _punchT = 0f;
            _punchDur = duration;
            _punchExtend = extend;
            _punchDmg = dmg;
            _punchReach = reach;
            _punchScale = scale;
        }

        void RefreshEnemies(bool force)
        {
            if (force || _enemies == null || Time.time - _lastScan > 0.2f)
            {
                _enemies = UnityEngine.Object.FindObjectsOfType<HealthManager>();
                _lastScan = Time.time;
            }
        }

        void Hit(Vector3 hp, float dir, int dmg, float reach)
        {
            RefreshEnemies(false);
            for (int i = 0; i < _enemies.Length; i++)
            {
                HealthManager hm = _enemies[i];
                if (hm == null || !hm.gameObject.activeInHierarchy || hm.hp <= 0) continue;
                Vector3 ep = hm.transform.position;
                float dx = (ep.x - hp.x) * dir;
                float dy = Mathf.Abs(ep.y - hp.y);
                if (dx > -0.5f && dx < reach && dy < PunchHeight) hm.ApplyExtraDamage(dmg);
            }
        }

        // ---------------- Quebrar objetos (como a magia) ----------------

        void TryBreak(GameObject target, GameObject source, float angle)
        {
            try
            {
                HitInstance hi = new HitInstance();
                hi.Source = source;
                hi.AttackType = AttackTypes.Spell;
                hi.DamageDealt = 15;
                hi.Direction = angle;
                hi.Multiplier = 1f;
                hi.MagnitudeMultiplier = 1f;
                hi.IgnoreInvulnerable = true;
                HitTaker.Hit(target, hi);
            }
            catch (Exception e)
            {
                LogError("Falha ao quebrar objeto: " + e.Message);
            }
        }

        void BreakInCircle(GameObject source, Vector2 center, float radius, float angle)
        {
            Breakable[] bs = UnityEngine.Object.FindObjectsOfType<Breakable>();
            for (int i = 0; i < bs.Length; i++)
            {
                if (bs[i] == null) continue;
                Vector2 bp = bs[i].transform.position;
                if (Vector2.Distance(bp, center) <= radius) TryBreak(bs[i].gameObject, source, angle);
            }
        }

        void BreakInColumn(GameObject source, float x, float yMin, float yMax, float halfWidth, float angle)
        {
            Breakable[] bs = UnityEngine.Object.FindObjectsOfType<Breakable>();
            for (int i = 0; i < bs.Length; i++)
            {
                if (bs[i] == null) continue;
                Vector2 bp = bs[i].transform.position;
                if (Mathf.Abs(bp.x - x) <= halfWidth && bp.y >= yMin && bp.y <= yMax) TryBreak(bs[i].gameObject, source, angle);
            }
        }

        // ---------------- Habilidades especiais ----------------

        void UseSpecial(HeroController hero, bool up, bool down)
        {
            PlayerData pd = PlayerData.instance;
            if (pd == null) return;
            int cost = SpecialSoulCost;
            if (_kind == Kind.Gold && up) cost = SpecialSoulCost * 2;
            if (pd.MPCharge < cost) return;

            if (_kind == Kind.Shadow)
            {
                hero.TakeMP(cost);
                if (up) ShadowFan(hero);
                else if (down) ShadowBlast(hero);
                else ShadowOrbs(hero);
            }
            else if (_kind == Kind.Star)
            {
                if (up)
                {
                    hero.TakeMP(cost);
                    StarFinger(hero);
                }
                else if (down)
                {
                    if (_juggleLeft > 0f) return;
                    hero.TakeMP(cost);
                    GroundSlam(hero);
                }
                else
                {
                    if (_worldLeft > 0f) return;
                    hero.TakeMP(cost);
                    StartTheWorld(hero);
                }
            }
            else if (_kind == Kind.Haze)
            {
                hero.TakeMP(cost);
                if (up) HazeCapsules(hero);
                else if (down) HazeOutbreak(hero);
                else HazeGas(hero);
            }
            else
            {
                if (pd.health >= pd.maxHealth) return;
                hero.TakeMP(cost);
                hero.AddHealth(up ? GoldHealUp : GoldHeal);
            }
        }

        // ---- Purple Haze ----
        // Neutro: nuvem de veneno a frente
        void HazeGas(HeroController hero)
        {
            float dir = hero.cState.facingRight ? 1f : -1f;
            Vector3 hp = hero.transform.position;
            MakeCloud(new Vector2(hp.x + dir * 3f, hp.y), GasRadius, GasLife, CloudDamage);
            StartPunch(0.2f, 2.4f, 0, PunchReach, 1.1f);
        }

        // Cima: tres capsulas de virus lancadas em arco; explodem em nuvens
        void HazeCapsules(HeroController hero)
        {
            float dir = hero.cState.facingRight ? 1f : -1f;
            Vector3 origin = _standGo != null ? _standGo.transform.position : hero.transform.position;
            if (_orbSprite == null) _orbSprite = BuildOrbSprite();
            Vector2[] dirs = new Vector2[]
            {
                new Vector2(dir * 0.5f, 1f).normalized,
                new Vector2(dir * 0.8f, 0.8f).normalized,
                new Vector2(dir * 1.0f, 0.5f).normalized
            };
            for (int i = 0; i < dirs.Length; i++)
            {
                Capsule c = new Capsule();
                c.Go = new GameObject("VirusCapsule");
                SpriteRenderer sr = c.Go.AddComponent<SpriteRenderer>();
                sr.sprite = _orbSprite;
                sr.color = new Color(0.7f, 1f, 0.4f, 1f);
                sr.sortingOrder = 6;
                c.Go.transform.position = new Vector3(origin.x + dirs[i].x, origin.y + 0.6f, origin.z);
                c.Vel = dirs[i] * CapsuleSpeed;
                c.Life = 1.2f;
                _capsules.Add(c);
            }
        }

        // Baixo: surto do virus em area grande ao redor
        void HazeOutbreak(HeroController hero)
        {
            Vector3 hp = hero.transform.position;
            MakeCloud(new Vector2(hp.x, hp.y), OutbreakRadius, OutbreakLife, OutbreakDamage);
            StartPunch(0.2f, 2.4f, 0, PunchReach, 1.1f);
        }

        void MakeCloud(Vector2 center, float radius, float life, int dmg)
        {
            if (_orbSprite == null) _orbSprite = BuildOrbSprite();
            Cloud c = new Cloud();
            c.Go = new GameObject("VirusCloud");
            c.Sr = c.Go.AddComponent<SpriteRenderer>();
            c.Sr.sprite = _orbSprite;
            c.Sr.sortingOrder = 4;
            float sc = radius * 2f / 0.75f;
            c.Go.transform.position = new Vector3(center.x, center.y, 0f);
            c.Go.transform.localScale = new Vector3(sc, sc, 1f);
            c.Center = center;
            c.Radius = radius;
            c.Life = life;
            c.Total = life;
            c.Tick = 0.1f;
            c.Dmg = dmg;
            _clouds.Add(c);
        }

        void UpdateClouds(float dt)
        {
            if (_clouds.Count == 0) return;
            RefreshEnemies(false);
            for (int i = _clouds.Count - 1; i >= 0; i--)
            {
                Cloud c = _clouds[i];
                if (c.Go == null) { _clouds.RemoveAt(i); continue; }
                c.Life -= dt;
                if (c.Life <= 0f)
                {
                    UnityEngine.Object.Destroy(c.Go);
                    _clouds.RemoveAt(i);
                    continue;
                }
                float fade = Mathf.Clamp01(c.Life / (c.Total * 0.3f));
                float pulse = 0.30f + Mathf.Sin(Time.time * 4f) * 0.05f;
                c.Sr.color = new Color(0.55f, 0.2f, 0.75f, pulse * fade);

                c.Tick -= dt;
                if (c.Tick <= 0f)
                {
                    c.Tick = CloudTick;
                    for (int k = 0; k < _enemies.Length; k++)
                    {
                        HealthManager hm = _enemies[k];
                        if (hm == null || !hm.gameObject.activeInHierarchy || hm.hp <= 0) continue;
                        Vector2 ep = hm.transform.position;
                        if (Vector2.Distance(ep, c.Center) <= c.Radius) hm.ApplyExtraDamage(c.Dmg);
                    }
                }
            }
        }

        void UpdateCapsules(float dt)
        {
            if (_capsules.Count == 0) return;
            RefreshEnemies(false);
            for (int i = _capsules.Count - 1; i >= 0; i--)
            {
                Capsule c = _capsules[i];
                if (c.Go == null) { _capsules.RemoveAt(i); continue; }
                c.Life -= dt;
                c.Vel.y -= CapsuleGravity * dt;
                c.Go.transform.position += new Vector3(c.Vel.x, c.Vel.y, 0f) * dt;
                Vector2 cp = c.Go.transform.position;

                bool boom = c.Life <= 0f;
                if (!boom && Physics2D.OverlapCircle(cp, 0.2f, 1 << 8) != null) boom = true;
                if (!boom)
                {
                    for (int k = 0; k < _enemies.Length; k++)
                    {
                        HealthManager hm = _enemies[k];
                        if (hm == null || !hm.gameObject.activeInHierarchy || hm.hp <= 0) continue;
                        Vector2 ep = hm.transform.position;
                        if (Vector2.Distance(cp, ep) < 1.2f) { boom = true; break; }
                    }
                }
                if (boom)
                {
                    MakeCloud(cp, CapsuleCloudRadius, CapsuleCloudLife, CloudDamage);
                    UnityEngine.Object.Destroy(c.Go);
                    _capsules.RemoveAt(i);
                }
            }
        }

        // ---- Sombra: magia ----
        void ShadowOrbs(HeroController hero)
        {
            float dir = hero.cState.facingRight ? 1f : -1f;
            Vector2[] dirs = new Vector2[]
            {
                new Vector2(dir, -0.12f).normalized,
                new Vector2(dir, 0f),
                new Vector2(dir, 0.12f).normalized
            };
            SpawnOrbs(hero, dirs);
        }

        void ShadowFan(HeroController hero)
        {
            float[] angles = new float[] { -40f, -20f, 0f, 20f, 40f };
            Vector2[] dirs = new Vector2[angles.Length];
            for (int i = 0; i < angles.Length; i++)
            {
                float r = angles[i] * Mathf.Deg2Rad;
                dirs[i] = new Vector2(Mathf.Sin(r), Mathf.Cos(r));
            }
            SpawnOrbs(hero, dirs);
        }

        void SpawnOrbs(HeroController hero, Vector2[] dirs)
        {
            Vector3 origin = _standGo != null ? _standGo.transform.position : hero.transform.position;
            if (_orbSprite == null) _orbSprite = BuildOrbSprite();
            for (int i = 0; i < dirs.Length; i++)
            {
                Orb o = new Orb();
                o.Go = new GameObject("SoulOrb");
                SpriteRenderer sr = o.Go.AddComponent<SpriteRenderer>();
                sr.sprite = _orbSprite;
                sr.sortingOrder = 6;
                o.Go.transform.position = new Vector3(origin.x + dirs[i].x * 1.2f, origin.y + 0.4f + dirs[i].y * 1.2f, origin.z);
                o.Vel = dirs[i] * OrbSpeed;
                o.Life = OrbLife;
                _orbs.Add(o);
            }
        }

        // Sombra mirando para baixo: explosao de alma no chao
        void ShadowBlast(HeroController hero)
        {
            Vector3 hp = hero.transform.position;
            Vector2 center = new Vector2(hp.x, hp.y - 0.8f);
            RefreshEnemies(true);
            for (int i = 0; i < _enemies.Length; i++)
            {
                HealthManager hm = _enemies[i];
                if (hm == null || !hm.gameObject.activeInHierarchy || hm.hp <= 0) continue;
                Vector2 ep = hm.transform.position;
                if (Vector2.Distance(ep, center) <= BlastRadius) hm.ApplyExtraDamage(BlastDamage);
            }
            BreakInCircle(hero.gameObject, center, BlastRadius, 270f);
            if (_orbSprite == null) _orbSprite = BuildOrbSprite();
            float s = BlastRadius * 2f / 0.75f;
            MakeFx(_orbSprite, new Vector3(center.x, center.y, hp.z), new Vector3(2f, 2f, 1f), new Vector3(s, s, 1f), 0.3f, new Color(0.6f, 0.8f, 1f, 0.8f));
        }

        // ---- Star Platinum: Star Finger (mirando para cima) ----
        void StarFinger(HeroController hero)
        {
            Vector3 hp = hero.transform.position;
            RefreshEnemies(true);
            for (int i = 0; i < _enemies.Length; i++)
            {
                HealthManager hm = _enemies[i];
                if (hm == null || !hm.gameObject.activeInHierarchy || hm.hp <= 0) continue;
                Vector3 ep = hm.transform.position;
                if (Mathf.Abs(ep.x - hp.x) <= FingerHalfWidth && ep.y >= hp.y - 1f && ep.y <= hp.y + FingerLength)
                    hm.ApplyExtraDamage(FingerDamage);
            }
            BreakInColumn(hero.gameObject, hp.x, hp.y, hp.y + FingerLength, FingerHalfWidth, 90f);

            if (_solidSprite == null) _solidSprite = BuildSolidSprite();
            MakeFx(_solidSprite,
                new Vector3(hp.x, hp.y + 1.5f + FingerLength / 2f, hp.z),
                new Vector3(0.35f, FingerLength, 1f), new Vector3(0.15f, FingerLength, 1f),
                0.3f, new Color(0.75f, 0.6f, 1f, 0.95f));
            _poseUp = 0.3f;
        }

        // ---- Star Platinum: soco no chao + malabarismo (mirando para baixo) ----
        void GroundSlam(HeroController hero)
        {
            float dir = hero.cState.facingRight ? 1f : -1f;
            Vector3 hp = hero.transform.position;
            Vector2 impact = new Vector2(hp.x + dir * 2f, hp.y - 1f);

            StartPunch(0.3f, 2.8f, 0, PunchReach, 1.3f, 1);
            BreakInCircle(hero.gameObject, impact, SlamRadius, 270f);
            if (_solidSprite == null) _solidSprite = BuildSolidSprite();
            MakeFx(_solidSprite, new Vector3(impact.x, impact.y, hp.z), new Vector3(1f, 0.3f, 1f), new Vector3(SlamRadius * 2f, 0.3f, 1f), 0.3f, new Color(0.8f, 0.7f, 1f, 0.8f));

            RefreshEnemies(true);
            _juggled.Clear();
            for (int i = 0; i < _enemies.Length; i++)
            {
                HealthManager hm = _enemies[i];
                if (hm == null || !hm.gameObject.activeInHierarchy || hm.hp <= 0) continue;
                Vector2 ep = hm.transform.position;
                if (Vector2.Distance(ep, impact) > SlamRadius) continue;

                Juggled j = new Juggled();
                j.Hm = hm;
                float ty = ep.y + JuggleHeight;
                RaycastHit2D r = Physics2D.Raycast(ep, Vector2.up, JuggleHeight + 1f, 1 << 8);
                if (r.collider != null) ty = Mathf.Max(ep.y, Mathf.Min(ty, r.point.y - 1f));
                j.TargetY = ty;
                j.Body = hm.GetComponent<Rigidbody2D>();
                if (j.Body != null)
                {
                    j.WasKinematic = j.Body.isKinematic;
                    j.Body.velocity = Vector2.zero;
                    j.Body.isKinematic = true;
                }
                hm.ApplyExtraDamage(SlamDamage);
                _juggled.Add(j);
            }
            if (_juggled.Count > 0)
            {
                _juggleLeft = JuggleTime;
                _juggleTick = 0.35f;
                Log("Soco no chao: " + _juggled.Count + " inimigos no ar");
            }
        }

        void UpdateJuggle(float dt)
        {
            if (_juggleLeft <= 0f) return;
            _juggleLeft -= dt;

            for (int i = 0; i < _juggled.Count; i++)
            {
                Juggled j = _juggled[i];
                if (j.Hm == null || j.Hm.hp <= 0) continue;
                Vector3 p = j.Hm.transform.position;
                float ny = Mathf.MoveTowards(p.y, j.TargetY, JuggleRiseSpeed * dt);
                j.Hm.transform.position = new Vector3(p.x, ny, p.z);
                if (j.Body != null) j.Body.velocity = Vector2.zero;
            }

            _juggleTick -= dt;
            if (_juggleTick <= 0f)
            {
                _juggleTick = JuggleHitInterval;
                bool any = false;
                for (int i = 0; i < _juggled.Count; i++)
                {
                    Juggled j = _juggled[i];
                    if (j.Hm == null || j.Hm.hp <= 0) continue;
                    j.Hm.ApplyExtraDamage(JuggleHitDamage);
                    any = true;
                }
                if (any) StartPunch(0.06f, 2.0f, 0, PunchReach, 1f, 2);
            }

            if (_juggleLeft <= 0f) EndJuggle();
        }

        void EndJuggle()
        {
            for (int i = 0; i < _juggled.Count; i++)
            {
                Juggled j = _juggled[i];
                if (j.Hm != null && j.Hm.hp > 0) j.Hm.ApplyExtraDamage(JuggleFinalDamage);
                if (j.Body != null)
                {
                    j.Body.isKinematic = j.WasKinematic;
                    if (!j.WasKinematic) j.Body.velocity = new Vector2(0f, -14f);
                }
            }
            _juggled.Clear();
            _juggleLeft = 0f;
            StartPunch(0.28f, 3.2f, 0, PunchReach, 1.2f, 1);
        }

        // ---- Star Platinum: The World (neutro) ----
        void StartTheWorld(HeroController hero)
        {
            Vector3 pos = hero.transform.position;
            _frozen.Clear();

            RefreshEnemies(true);
            for (int i = 0; i < _enemies.Length; i++)
            {
                HealthManager hm = _enemies[i];
                if (hm == null || !hm.gameObject.activeInHierarchy || hm.hp <= 0) continue;
                if (Vector3.Distance(pos, hm.transform.position) > WorldRange) continue;

                Frozen f = new Frozen();
                f.Hm = hm;
                f.Fsms = hm.GetComponents<PlayMakerFSM>();
                f.FsmWasEnabled = new bool[f.Fsms.Length];
                for (int k = 0; k < f.Fsms.Length; k++)
                {
                    f.FsmWasEnabled[k] = f.Fsms[k].enabled;
                    f.Fsms[k].enabled = false;
                }
                f.Body = hm.GetComponent<Rigidbody2D>();
                if (f.Body != null)
                {
                    f.WasKinematic = f.Body.isKinematic;
                    f.Body.velocity = Vector2.zero;
                    f.Body.isKinematic = true;
                }
                _frozen.Add(f);
            }
            _worldLeft = WorldDuration;
            Log("ZA WARUDO! " + _frozen.Count + " inimigos congelados");
        }

        // Inimigo que morre durante o time stop precisa do evento de morte
        void ReviveDead()
        {
            for (int i = 0; i < _frozen.Count; i++)
            {
                Frozen f = _frozen[i];
                if (f.Revived || f.Hm == null || f.Hm.hp > 0) continue;
                f.Revived = true;
                for (int k = 0; k < f.Fsms.Length; k++)
                    if (f.Fsms[k] != null) f.Fsms[k].enabled = f.FsmWasEnabled[k];
                if (f.Body != null) f.Body.isKinematic = f.WasKinematic;
                FSMUtility.SendEventToGameObject(f.Hm.gameObject, "ZERO HP");
            }
        }

        void EndTheWorld()
        {
            for (int i = 0; i < _frozen.Count; i++)
            {
                Frozen f = _frozen[i];
                if (f.Revived) continue;
                for (int k = 0; k < f.Fsms.Length; k++)
                    if (f.Fsms[k] != null) f.Fsms[k].enabled = f.FsmWasEnabled[k];
                if (f.Body != null) f.Body.isKinematic = f.WasKinematic;
            }
            _frozen.Clear();
            _worldLeft = 0f;
        }

        // ---------------- Orbes e efeitos ----------------

        void UpdateOrbs(float dt)
        {
            if (_orbs.Count == 0) return;
            RefreshEnemies(false);
            for (int i = _orbs.Count - 1; i >= 0; i--)
            {
                Orb o = _orbs[i];
                bool remove = false;
                if (o.Go == null) remove = true;
                else
                {
                    o.Life -= dt;
                    o.Go.transform.position += new Vector3(o.Vel.x, o.Vel.y, 0f) * dt;
                    Vector2 op = o.Go.transform.position;
                    for (int k = 0; k < _enemies.Length; k++)
                    {
                        HealthManager hm = _enemies[k];
                        if (hm == null || !hm.gameObject.activeInHierarchy || hm.hp <= 0) continue;
                        Vector2 ep = hm.transform.position;
                        if (Vector2.Distance(op, ep) < 1.3f)
                        {
                            hm.ApplyExtraDamage(OrbDamage);
                            remove = true;
                            break;
                        }
                    }
                    if (o.Life <= 0f) remove = true;
                }
                if (remove)
                {
                    if (o.Go != null) UnityEngine.Object.Destroy(o.Go);
                    _orbs.RemoveAt(i);
                }
            }
        }

        void MakeFx(Sprite spr, Vector3 pos, Vector3 s0, Vector3 s1, float life, Color c)
        {
            Fx f = new Fx();
            f.Go = new GameObject("StandFx");
            f.Sr = f.Go.AddComponent<SpriteRenderer>();
            f.Sr.sprite = spr;
            f.Sr.sortingOrder = 7;
            f.Sr.color = c;
            f.Go.transform.position = pos;
            f.Go.transform.localScale = s0;
            f.Life = life;
            f.Total = life;
            f.S0 = s0;
            f.S1 = s1;
            f.C = c;
            _fx.Add(f);
        }

        void UpdateFx(float dt)
        {
            for (int i = _fx.Count - 1; i >= 0; i--)
            {
                Fx f = _fx[i];
                if (f.Go == null) { _fx.RemoveAt(i); continue; }
                f.Life -= dt;
                if (f.Life <= 0f)
                {
                    UnityEngine.Object.Destroy(f.Go);
                    _fx.RemoveAt(i);
                    continue;
                }
                float t = 1f - f.Life / f.Total;
                f.Go.transform.localScale = Vector3.Lerp(f.S0, f.S1, t);
                f.Sr.color = new Color(f.C.r, f.C.g, f.C.b, f.C.a * (1f - t));
            }
        }

        // ---------------- Sprites (desenhados por codigo) ----------------

        static void Rect(Color32[] px, int w, int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                    px[y * w + x] = c;
        }

        static Sprite Finish(Color32[] px, int W, int H, Color32 outline, float ppu)
        {
            Color32[] outlined = (Color32[])px.Clone();
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    if (px[y * W + x].a != 0) continue;
                    bool near = (x > 0 && px[y * W + x - 1].a != 0) || (x < W - 1 && px[y * W + x + 1].a != 0)
                             || (y > 0 && px[(y - 1) * W + x].a != 0) || (y < H - 1 && px[(y + 1) * W + x].a != 0);
                    if (near) outlined[y * W + x] = outline;
                }
            }
            Texture2D tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels32(outlined);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            Sprite s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), ppu);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        static Sprite BuildSprite(Kind kind)
        {
            const int W = 32;
            const int H = 48;
            Color32[] px = new Color32[W * H];

            if (kind == Kind.Shadow)
            {
                Color32 B = new Color32(18, 18, 26, 255);
                Color32 Wh = new Color32(240, 240, 255, 255);
                Color32 N = new Color32(200, 200, 220, 255);
                Rect(px, W, 12, 0, 15, 9, B);
                Rect(px, W, 17, 0, 20, 9, B);
                Rect(px, W, 8, 10, 24, 16, B);
                Rect(px, W, 9, 17, 23, 29, B);
                Rect(px, W, 11, 30, 21, 40, B);
                Rect(px, W, 9, 40, 12, 46, B);
                Rect(px, W, 20, 40, 23, 46, B);
                Rect(px, W, 13, 34, 15, 37, Wh);
                Rect(px, W, 17, 34, 19, 37, Wh);
                Rect(px, W, 22, 20, 24, 22, B);
                Rect(px, W, 25, 14, 26, 31, N);
                return Finish(px, W, H, new Color32(70, 70, 110, 255), 22f);
            }

            Color32 P, D, L, E;
            if (kind == Kind.Star)
            {
                P = new Color32(110, 80, 200, 255);
                D = new Color32(35, 20, 80, 255);
                L = new Color32(160, 130, 235, 255);
                E = new Color32(80, 230, 255, 255);
            }
            else if (kind == Kind.Gold)
            {
                P = new Color32(230, 190, 60, 255);
                D = new Color32(110, 70, 10, 255);
                L = new Color32(255, 230, 130, 255);
                E = new Color32(60, 200, 80, 255);
            }
            else
            {
                P = new Color32(125, 45, 150, 255);
                D = new Color32(45, 10, 60, 255);
                L = new Color32(185, 100, 205, 255);
                E = new Color32(140, 255, 80, 255);
            }
            Rect(px, W, 10, 0, 15, 15, P);
            Rect(px, W, 17, 0, 22, 15, P);
            Rect(px, W, 10, 0, 16, 2, D);
            Rect(px, W, 17, 0, 23, 2, D);
            Rect(px, W, 8, 16, 24, 31, P);
            Rect(px, W, 11, 22, 21, 30, L);
            Rect(px, W, 8, 16, 24, 18, D);
            Rect(px, W, 4, 18, 8, 29, D);
            Rect(px, W, 22, 24, 28, 29, P);
            Rect(px, W, 27, 23, 31, 30, L);
            Rect(px, W, 11, 32, 21, 42, P);
            Rect(px, W, 13, 33, 20, 39, L);
            Rect(px, W, 17, 36, 19, 37, E);
            Rect(px, W, 10, 41, 22, 46, D);
            Rect(px, W, 8, 44, 12, 47, D);
            if (kind == Kind.Haze)
            {
                Color32 G = new Color32(150, 255, 90, 255); // capsulas de virus
                Rect(px, W, 12, 24, 14, 29, G);
                Rect(px, W, 17, 24, 19, 29, G);
                Rect(px, W, 5, 20, 7, 24, G);
            }
            return Finish(px, W, H, D, 22f);
        }

        static Sprite BuildSolidSprite()
        {
            const int S = 4;
            Color32[] px = new Color32[S * S];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            Sprite s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 4f);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }

        static Sprite BuildOrbSprite()
        {
            const int S = 12;
            Color32[] px = new Color32[S * S];
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x - 5.5f;
                    float dy = y - 5.5f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    if (d <= 3f) px[y * S + x] = new Color32(235, 250, 255, 255);
                    else if (d <= 5.5f) px[y * S + x] = new Color32(120, 200, 255, 220);
                }
            }
            Texture2D tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixels32(px);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            Sprite s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 16f);
            s.hideFlags = HideFlags.HideAndDontSave;
            return s;
        }
    }
}
