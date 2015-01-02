﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using System.IO;
using SharpDX;
using Collision = LeagueSharp.Common.Collision;
namespace Jinx
{
    class Program
    {
        public const string ChampionName = "Jinx";

        //Orbwalker instance
        public static Orbwalking.Orbwalker Orbwalker;
        //Spells
        public static List<Spell> SpellList = new List<Spell>();
        public static Spell Q;
        public static Spell W;
        public static Spell E;
        public static Spell R;
        //ManaMenager
        public static int QMANA;
        public static int WMANA;
        public static int EMANA;
        public static int RMANA;
        public static bool Farm = false;
        //AutoPotion
        public static Items.Item Potion = new Items.Item(2003, 0);
        public static Items.Item ManaPotion = new Items.Item(2004, 0);
        public static Items.Item Youmuu = new Items.Item(3142, 0);
        //Menu
        public static Menu Config;

        private static Obj_AI_Hero Player;

        private static void Main(string[] args)
        {
            CustomEvents.Game.OnGameLoad += Game_OnGameLoad;
        }
        private static void Game_OnGameLoad(EventArgs args)
        {
            Player = ObjectManager.Player;
            if (Player.BaseSkinName != ChampionName) return;

            //Create the spells
            Q = new Spell(SpellSlot.Q, float.MaxValue);
            W = new Spell(SpellSlot.W, 1500f);
            E = new Spell(SpellSlot.E, 900f);
            R = new Spell(SpellSlot.R, 25000f);
            W.SetSkillshot(0.6f, 60f, 3300f, true, SkillshotType.SkillshotLine);
            E.SetSkillshot(1.1f, 1f, 1750f, false, SkillshotType.SkillshotCircle);
            R.SetSkillshot(0.6f, 140f, 1500f, false, SkillshotType.SkillshotLine);

            SpellList.Add(Q);
            SpellList.Add(W);
            SpellList.Add(E);
            SpellList.Add(R);

            //Create the menu
            Config = new Menu(ChampionName, ChampionName, true);

            var targetSelectorMenu = new Menu("Target Selector", "Target Selector");
            TargetSelector.AddToMenu(targetSelectorMenu);
            Config.AddSubMenu(targetSelectorMenu);

            //Orbwalker submenu
            Config.AddSubMenu(new Menu("Orbwalking", "Orbwalking"));

            //Load the orbwalker and add it to the submenu.
            Orbwalker = new Orbwalking.Orbwalker(Config.SubMenu("Orbwalking"));
            Config.AddToMainMenu();
            Config.AddItem(new MenuItem("autoR", "Auto R").SetValue(true));
            Config.AddItem(new MenuItem("useR", "Semi-manual cast R Key").SetValue(new KeyBind('t', KeyBindType.Press))); //32 == space
            //Add the events we are going to use:
            Drawing.OnDraw += Drawing_OnDraw;
            Game.OnGameUpdate += Game_OnGameUpdate;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
            Game.PrintChat("<font color=\"#ff00d8\">J</font>inx full automatic SI ver 1.2 <font color=\"#000000\">by sebastiank1</font> - <font color=\"#00BFFF\">Loaded</font>");
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            ManaMenager();
            PotionMenager();
            
            if (Orbwalker.ActiveMode.ToString() == "Mixed" || Orbwalker.ActiveMode.ToString() == "LaneClear")
                Farm = true;
            else
                Farm = false;

            if (ObjectManager.Player.Mana > RMANA + EMANA && E.IsReady())
            {

                var t = TargetSelector.GetTarget(900f, TargetSelector.DamageType.Physical);

                var autoEi = true;
                var autoEs = true;
                var autoEd = true;
                foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(E.Range)))
                {
                    if (autoEs && enemy.HasBuffOfType(BuffType.Slow) &&  enemy.GetWaypoints().Count > 1)
                    {
                        E.CastIfHitchanceEquals(t, HitChance.VeryHigh, true);
                    }
                    else if (autoEi &&
                        (enemy.HasBuffOfType(BuffType.Stun) || enemy.HasBuffOfType(BuffType.Snare) ||
                         enemy.HasBuffOfType(BuffType.Charm) || enemy.HasBuffOfType(BuffType.Fear) ||
                         enemy.HasBuffOfType(BuffType.Taunt)|| enemy.IsStunned))
                        E.CastIfHitchanceEquals(enemy, HitChance.High, true);
                }
            }
            
            if (Q.IsReady())
            {
                if (Farm)
                    if (ObjectManager.Player.Mana > RMANA + WMANA + EMANA && !FishBoneActive)
                        farmQ();
                var t = TargetSelector.GetTarget(bonusRange() , TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                {
                    var distance = GetRealDistance(t);
                    var powPowRange = GetRealPowPowRange(t);
                    if (Youmuu.IsReady() && (ObjectManager.Player.GetAutoAttackDamage(t) * 6 > t.Health || ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.4))
                        Youmuu.Cast();
                    if (!FishBoneActive && (distance > powPowRange) && (ObjectManager.Player.Mana > RMANA + WMANA || ObjectManager.Player.GetAutoAttackDamage(t) > t.Health))
                    {
                        if (Orbwalker.ActiveMode.ToString() == "Combo")
                            Q.Cast();
                        else if (Farm && harasQ() && ObjectManager.Player.Mana > RMANA + WMANA + EMANA + WMANA)
                            if (distance < bonusRange() + 120 && ObjectManager.Player.Path.Count() > 0 )
                                Q.Cast();
                            else if ( distance < bonusRange() + 70)
                                Q.Cast();
                    }
                    else if (Orbwalker.ActiveMode.ToString() == "Combo" && FishBoneActive && (distance < powPowRange))
                        Q.Cast();
                    else if (Farm && FishBoneActive && (distance > bonusRange() || distance < powPowRange))
                         Q.Cast();
                }
                else if (FishBoneActive && Farm)
                    Q.Cast();
                else if (!FishBoneActive && (Orbwalker.ActiveMode.ToString() == "Combo"))
                    Q.Cast();
            }

            if (W.IsReady())
            {
                var t = TargetSelector.GetTarget(W.Range, TargetSelector.DamageType.Physical);
                if (t.IsValidTarget())
                {
                    var wDmg = W.GetDamage(t);

                    if (GetRealDistance(t) > GetRealPowPowRange(t) && wDmg + ObjectManager.Player.GetAutoAttackDamage(t) > t.Health)
                        W.Cast(t, true);
                    else if (Orbwalker.ActiveMode.ToString() == "Combo" && ObjectManager.Player.Mana > RMANA + WMANA && CountEnemies(ObjectManager.Player, GetRealPowPowRange(t)) == 0)
                        W.CastIfHitchanceEquals(t, HitChance.High, true);
                    else if ((Farm && ObjectManager.Player.Mana > RMANA + EMANA + WMANA + WMANA) && CountEnemies(ObjectManager.Player, GetRealPowPowRange(t)) == 0 && t.Path.Count() > 0 )
                        W.CastIfHitchanceEquals(t, HitChance.VeryHigh, true);
                    else if (Orbwalker.ActiveMode.ToString() == "Combo" && Farm && ObjectManager.Player.Mana > RMANA + WMANA && CountEnemies(ObjectManager.Player, GetRealPowPowRange(t)) == 0)
                    {
                        foreach (var enemy in ObjectManager.Get<Obj_AI_Hero>().Where(enemy => enemy.IsValidTarget(W.Range)))
                        {
                            if (enemy.HasBuffOfType(BuffType.Stun) || enemy.HasBuffOfType(BuffType.Snare) ||
                             enemy.HasBuffOfType(BuffType.Charm) || enemy.HasBuffOfType(BuffType.Fear) ||
                             enemy.HasBuffOfType(BuffType.Taunt) || enemy.HasBuffOfType(BuffType.Slow))
                                W.CastIfHitchanceEquals(t, HitChance.High, true);
                        }
                    }
                }
            }

            if (R.IsReady())
            {
                bool cast = true;
                var maxR = 2500f;
                var target = TargetSelector.GetTarget(maxR, TargetSelector.DamageType.Physical);
                float predictedHealth = HealthPrediction.GetHealthPrediction(target, (int)(R.Delay + (Player.Distance(target) / R.Speed) * 1000));

                if (target.IsValidTarget() && Config.Item("autoR").GetValue<bool>())
                {
                    if (R.GetDamage(target) > predictedHealth && CountAlliesNearTarget(target, 500) == 0 && GetRealDistance(target) > bonusRange() + 70 )
                    {

                        PredictionOutput output = R.GetPrediction(target);

                        Vector2 direction = output.CastPosition.To2D() - Player.Position.To2D();
                        direction.Normalize();

                        List<Obj_AI_Hero> enemies = ObjectManager.Get<Obj_AI_Hero>().Where(x => x.IsEnemy && x.IsValidTarget()).ToList();
                        foreach (var enemy in enemies)
                        {
                            if (enemy.SkinName == target.SkinName || !cast)
                                continue;

                            PredictionOutput prediction = R.GetPrediction(enemy);
                            Vector3 predictedPosition = prediction.CastPosition;

                            Vector3 v = output.CastPosition - Player.Position;
                            Vector3 w = predictedPosition - Player.Position;

                            double c1 = Vector3.Dot(w, v);
                            double c2 = Vector3.Dot(v, v);
                            double b = c1 / c2;

                            Vector3 pb = Player.Position + ((float)b * v);
                            float length = Vector3.Distance(predictedPosition, pb);

                            if (length < (R.Width + enemy.BoundingRadius / 2) && Player.Distance(predictedPosition) < Player.Distance(target))
                                cast = false;
                        }
                        if (cast && target.IsValidTarget())
                            R.Cast(target, true);
                    }
                    
                    /*
                    var distance = GetRealDistance(t);
                    var rDamage = R.GetDamage(t);
                    var powPowRange = GetRealPowPowRange(t);
                    if (rDamage > t.Health && CountAlliesNearTarget(t, 600) == 0 && CountEnemies(ObjectManager.Player, 200f) == 0 && distance > bonusRange() + 70 && t.Path.Count() > 1)
                            R.CastIfHitchanceEquals(t, HitChance.VeryHigh, true);
                    else if (ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.4 && rDamage * 1.4 > t.Health && CountEnemies(ObjectManager.Player, GetRealPowPowRange(t)) > 0 && distance > 300)
                        R.CastIfHitchanceEquals(t, HitChance.VeryHigh, true);
                    else if (rDamage * 1.4 > t.Health && CountEnemies(t, 200) > 2)
                        R.CastIfHitchanceEquals(t, HitChance.VeryHigh, true);
                     * */
                }
                if (target.IsValidTarget() && Config.Item("useR").GetValue<KeyBind>().Active)
                {
                    R.CastIfHitchanceEquals(target, HitChance.High, true);
                }
            }
        }
       
        public static void farmQ()
        {
            var allMinionsQ = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, bonusRange() + 30, MinionTypes.All);
            foreach (var minion in allMinionsQ)
            {
                if (!Orbwalking.InAutoAttackRange(minion) && minion.Health < ObjectManager.Player.GetAutoAttackDamage(minion) && GetRealPowPowRange(minion) < GetRealDistance(minion) && bonusRange() < GetRealDistance(minion))
                {
                    Q.Cast();
                    return;
                }
                else if (Orbwalking.InAutoAttackRange(minion) && CountEnemies(minion, 150) > 0)
                {
                    Q.Cast();
                    return;
                }
            }
        }
        public static bool harasQ()
        {
            var allMinionsQ = MinionManager.GetMinions(ObjectManager.Player.ServerPosition, GetRealPowPowRange(ObjectManager.Player), MinionTypes.All);
            foreach (var minion in allMinionsQ)
            {
                if (minion.Health < ObjectManager.Player.GetAutoAttackDamage(minion) && GetRealPowPowRange(minion) > GetRealDistance(minion))
                    return false;
            }
            return true;
        }
        public static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base unit, GameObjectProcessSpellCastEventArgs args)
        {
            double ShouldUse = ShouldUseE(args.SData.Name);
            if (unit.Team != ObjectManager.Player.Team && ShouldUse >= 0f)
                E.Cast(unit, true);
        }

        public static double ShouldUseE(string SpellName)
        {
            if (SpellName == "ThreshQ")
                return 0;
            if (SpellName == "KatarinaR")
                return 0;
            if (SpellName == "AlZaharNetherGrasp")
                return 0;
            if (SpellName == "GalioIdolOfDurand")
                return 0;
            if (SpellName == "LuxMaliceCannon")
                return 0;
            if (SpellName == "MissFortuneBulletTime")
                return 0;
            if (SpellName == "RocketGrabMissile")
                return 0;
            if (SpellName == "CaitlynPiltoverPeacemaker")
                return 0;
            if (SpellName == "EzrealTrueshotBarrage")
                return 0;
            if (SpellName == "InfiniteDuress")
                return 0;
            if (SpellName == "VelkozR")
                return 0;
            return -1;
        }

        public static float bonusRange()
        {
            return 620f + ObjectManager.Player.BoundingRadius + 50 + 25 * ObjectManager.Player.Spellbook.GetSpell(SpellSlot.Q).Level; 
        }

        private static bool FishBoneActive
        {
            get { return Math.Abs(ObjectManager.Player.AttackRange - 525f) > float.Epsilon; }
        }

        private static int PowPowStacks
        {
            get
            {
                return
                    ObjectManager.Player.Buffs.Where(buff => buff.DisplayName.ToLower() == "jinxqramp")
                        .Select(buff => buff.Count)
                        .FirstOrDefault();
            }
        }
        private static int CountEnemies(Obj_AI_Base target, float range)
        {
            return
                ObjectManager.Get<Obj_AI_Hero>()
                    .Count(
                        hero =>
                            hero.IsValidTarget() && hero.Team != ObjectManager.Player.Team &&
                            hero.ServerPosition.Distance(target.ServerPosition) <= range);
        }
        private static int CountAlliesNearTarget(Obj_AI_Base target, float range)
        {
            return
                ObjectManager.Get<Obj_AI_Hero>()
                    .Count(
                        hero =>
                            hero.Team == ObjectManager.Player.Team &&
                            hero.ServerPosition.Distance(target.ServerPosition) <= range);
        }

        private static float GetRealPowPowRange(GameObject target)
        {
            return 600f + ObjectManager.Player.BoundingRadius + target.BoundingRadius;
        }

        private static float GetRealDistance(GameObject target)
        {
            return ObjectManager.Player.Position.Distance(target.Position) + ObjectManager.Player.BoundingRadius +
                   target.BoundingRadius;
        }

        private static float GetSlowEndTime(Obj_AI_Base target)
        {
            return
                target.Buffs.OrderByDescending(buff => buff.EndTime - Game.Time)
                    .Where(buff => buff.Type == BuffType.Slow)
                    .Select(buff => buff.EndTime)
                    .FirstOrDefault();
        }

        public static bool InFountain()
        {
            float fountainRange = 750;
            if (Utility.Map.GetMap()._MapType == Utility.Map.MapType.SummonersRift)
                fountainRange = 1050;
            return ObjectManager.Get<Obj_SpawnPoint>()
                    .Where(spawnPoint => spawnPoint.IsAlly)
                    .Any(spawnPoint => Vector2.Distance(ObjectManager.Player.Position.To2D(), spawnPoint.Position.To2D()) < fountainRange);
        }

        public static void ManaMenager()
        {
            QMANA = 10;
            WMANA = 40 + 10 * W.Level;
            EMANA = 50;
            if (!R.IsReady())
                RMANA = WMANA - 10;
            else
                RMANA = 100;
        }

        public static void PotionMenager()
        {
            if (Potion.IsReady() && !InFountain() && !ObjectManager.Player.HasBuff("RegenerationPotion", true))
            {
                if (CountEnemies(ObjectManager.Player, 700) > 0 && ObjectManager.Player.Health + 200 < ObjectManager.Player.MaxHealth)
                    Potion.Cast();
                else if (ObjectManager.Player.Health < ObjectManager.Player.MaxHealth * 0.6)
                    Potion.Cast();
            }
            if (ManaPotion.IsReady() && !InFountain())
            {
                if (CountEnemies(ObjectManager.Player, 1000) > 0 && ObjectManager.Player.Mana < RMANA + WMANA + EMANA)
                    ManaPotion.Cast();
            } 
        }
        private static void Drawing_OnDraw(EventArgs args)
        {
            if (R.IsReady())
            {
                var maxR = 2500f;
                var t = TargetSelector.GetTarget(maxR, TargetSelector.DamageType.Physical);
                float predictedHealth = HealthPrediction.GetHealthPrediction(t, (int)(R.Delay + (Player.Distance(t) / R.Speed) * 1000));
                if (t.IsValidTarget())
                {
                    var rDamage = R.GetDamage(t);
                    if (rDamage > predictedHealth)
                        Drawing.DrawText(Drawing.Width * 0.1f, Drawing.Height * 0.8f, System.Drawing.Color.Red, "Ult can kill: " + t.ChampionName);
                }
            }
        }
    }
}
