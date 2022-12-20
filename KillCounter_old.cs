using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Abstract;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using ExileCore.Shared.Static;
using SharpDX;

namespace KillCounter
{
    public class KillCounter : BaseSettingsPlugin<KillCounterSettings>
    {
        private bool _canRender;
        private Dictionary<uint, HashSet<long>> countedIds;
        private Dictionary<MonsterRarity, int> counters;
        private int sessionCounter;
        private int summaryCounter;
        private Dictionary<string, string> generals;
        private List<string> bossNames;
        private Dictionary<string, int> bossCounters;
        private List<string> jewelNames;
        private Dictionary<string, int> jewelCounters;
        private List<string> emblemNames;
        private Dictionary<string, int> emblemCounters;
        private AreaInstance previousAreaInstance;
        private Dictionary<string, BossInfo> times;

        class BossInfo
        {
            public bool Draw = true;
            public float EntityZ;
            public Vector2 GridPos;
            public long LongID;
            public DateTime Time;
            public MinimapTextInfo TextureInfo;

            public BossInfo(DateTime time, float entityZ, Vector2 gridPos, MinimapTextInfo textureInfo)
            {
                Time = time;
                EntityZ = entityZ;
                GridPos = gridPos;
                TextureInfo = textureInfo;
            }
        }

        public override bool Initialise()
        {
            GameController.LeftPanel.WantUse(() => Settings.Enable);
            countedIds = new Dictionary<uint, HashSet<long>>();
            counters = new Dictionary<MonsterRarity, int>();
            bossNames = new List<string>
            {
                "Vox", "Hyrri", "Viper", "Marceus", "Aukuna"
            };
            jewelNames = new List<string>
            {
                "Militant Faith",
                "Lethal Pride",
                "Glorious Vanity",
                "Elegant Hubris",
                "Brutal Restraint"
            };
            emblemNames = new List<string>
            {
                "Unrelenting Timeless Templar Emblem",
                "Unrelenting Timeless Karui Emblem",
                "Unrelenting Timeless Vaal Emblem",
                "Unrelenting Timeless Eternal Emblem",
                "Unrelenting Timeless Maraketh Emblem"
            };
            ResetCounters();
            generals = new Dictionary<string, string>
            {
                {"Metadata/Monsters/LegionLeague/LegionTemplarGeneral", bossNames[0]},
                {"Metadata/Monsters/LegionLeague/LegionKaruiGeneral", bossNames[1]},
                {"Metadata/Monsters/LegionLeague/LegionVaalGeneral", bossNames[2]},
                {"Metadata/Monsters/LegionLeague/LegionEternalEmpireGeneral", bossNames[3]},
                {"Metadata/Monsters/LegionLeague/LegionMarakethGeneralDismounted", bossNames[4]}
            };
            var textInfo = new MinimapTextInfo
            {
                FontSize = 10,
                FontColor = Color.White,
                TextWrapLength = 50
            };
            times = new Dictionary<string, BossInfo>
            {
                {bossNames[0], new BossInfo(DateTime.Now, 0, Vector2.Zero, textInfo)},
                {bossNames[1], new BossInfo(DateTime.Now, 0, Vector2.Zero, textInfo)},
                {bossNames[2], new BossInfo(DateTime.Now, 0, Vector2.Zero, textInfo)},
                {bossNames[3], new BossInfo(DateTime.Now, 0, Vector2.Zero, textInfo)},
                {bossNames[4], new BossInfo(DateTime.Now, 0, Vector2.Zero, textInfo)}
            };
            Init();
            return true;
        }

        private void ResetCounters()
        {
            bossCounters = new Dictionary<string, int>();
            foreach (var bossName in bossNames)
            {
                bossCounters[bossName] = 0;
            }

            jewelCounters = new Dictionary<string, int>();
            foreach (var jewelName in jewelNames)
            {
                jewelCounters[jewelName] = 0;
            }

            emblemCounters = new Dictionary<string, int>();
            foreach (var emblemName in emblemNames)
            {
                emblemCounters[emblemName] = 0;
            }
        }

        public override void OnLoad()
        {
            CanUseMultiThreading = true;
            Order = -10;
            Graphics.InitImage("preload-new.png");
        }

        private void Init()
        {
            foreach (MonsterRarity rarity in Enum.GetValues(typeof(MonsterRarity)))
            {
                counters[rarity] = 0;
            }
        }

        public override void AreaChange(AreaInstance area)
        {
            if (!Settings.Enable.Value) return;
            countedIds.Clear();
            counters.Clear();

            var bossesKilled = bossCounters.Sum(b => b.Value);
            DebugWindow.LogDebug($"Bossess killed: {bossesKilled}");
            if (previousAreaInstance != null && previousAreaInstance.Name.Contains("Domain") && bossesKilled > 0)
            {
                var items = GameController.IngameState.Data.ServerData.PlayerInventories[0]?.Inventory?.Items;
                if (items != null)
                {
                    var jewels = items.Where(i => i.Metadata.Contains("JewelTimeless")).ToList();
                    var emblems = items.Where(e =>
                        e.Metadata.Contains("CurrencyLegionFragment") && e.Metadata.Contains("Uber"));
                    foreach (var jewel in jewels)
                    {
                        var jewelBase = jewel.GetComponent<Base>();
                        var mods = jewel.GetComponent<Mods>();
                        DebugWindow.LogDebug($"{jewelBase.Name} ({mods.UniqueName})");
                        jewelCounters[mods.UniqueName]++;
                    }

                    foreach (var emblem in emblems)
                    {
                        var emblemBase = emblem.GetComponent<Base>();
                        DebugWindow.LogDebug($"{emblemBase.Name}");
                        emblemCounters[emblemBase.Name]++;
                    }
                }

                if (!File.Exists(Settings.LegionFile))
                {
                    var namesList = bossNames.Concat(jewelNames).Concat(emblemNames);
                    File.AppendAllText(Settings.LegionFile, $"{string.Join("\t", namesList)}{Environment.NewLine}");
                }

                var bossCount = bossNames.Select(bossName => bossCounters[bossName]).ToList();
                var jewelCount = jewelNames.Select(jewelName => jewelCounters[jewelName]).ToList();
                var emblemCount = emblemNames.Select(emblemName => emblemCounters[emblemName]).ToList();
                var countList = bossCount.Concat(jewelCount).Concat(emblemCount);
                File.AppendAllText(Settings.LegionFile, $"{string.Join("\t", countList)}{Environment.NewLine}");
            }

            ResetCounters();
            sessionCounter += summaryCounter;
            summaryCounter = 0;
            previousAreaInstance = area;
            Init();
        }

        public override Job Tick()
        {
            if (Settings.MultiThreading)
                return GameController.MultiThreadManager.AddJob(TickLogic, nameof(KillCounter));

            TickLogic();
            return null;
        }

        private void TickLogic()
        {
            foreach (var entity in GameController.EntityListWrapper.ValidEntitiesByType[EntityType.Monster])
            {
                if (entity.IsAlive) continue;
                Calc(entity);
            }
        }

        public override void Render()
        {
            var UIHover = GameController.Game.IngameState.UIHover;
            var miniMap = GameController.Game.IngameState.IngameUi.Map.SmallMiniMap;

            if (Settings.Enable.Value && UIHover.Address != 0x00 && UIHover.Tooltip.Address != 0x00 &&
                UIHover.Tooltip.IsVisibleLocal &&
                UIHover.Tooltip.GetClientRect().Intersects(miniMap.GetClientRect()))
                _canRender = false;

            if (UIHover.Address == 0x00 || UIHover.Tooltip.Address == 0x00 || !UIHover.Tooltip.IsVisibleLocal)
                _canRender = true;

            if (!Settings.Enable || Input.GetKeyState(Keys.F10) || GameController.Area.CurrentArea == null ||
                !Settings.ShowInTown && GameController.Area.CurrentArea.IsTown ||
                !Settings.ShowInTown && GameController.Area.CurrentArea.IsHideout) return;

            if (!_canRender) return;

            var position = GameController.LeftPanel.StartDrawPoint;
            var size = Vector2.Zero;

            if (Settings.ShowDetail) size = DrawCounters(position);
            var session = $"({sessionCounter + summaryCounter})";
            position.Y += size.Y;

            var size2 = Graphics.DrawText($"kills: {summaryCounter} {session}", position.Translate(0, 5),
                Settings.TextColor,
                FontAlign.Right);

            var width = Math.Max(size.X, size2.X);
            var bounds = new RectangleF(position.X - width - 50, position.Y - size.Y, width + 50,
                size.Y + size2.Y + 10);
            Graphics.DrawImage("preload-new.png", bounds, Settings.BackgroundColor);

            if (GameController.Area.CurrentArea.Name
                .Contains("Domain") /*!GameController.Area.CurrentArea.IsHideout && !GameController.Area.CurrentArea.IsTown*/
               )
            {
                var bossPos = new Vector2(200, 280);
                foreach (var bossName in bossNames)
                {
                    var count = bossCounters[bossName];
                    Graphics.DrawText($"{bossName}: {count}", bossPos);
                    var seconds = (times[bossName].Time - DateTime.Now).TotalSeconds;
                    if (seconds < 0)
                    {
                        seconds = 0;
                    }

                    if (Settings.DrawTime&&GameController.Game.IngameState.IngameUi.Map.LargeMap.IsVisible)
                    {
                        DrawToLargeMiniMapText(times[bossName], times[bossName].TextureInfo);
                    }

                    Graphics.DrawText($"{seconds} sec", bossPos.Translate(Settings.TimePosition));
                    bossPos.Y += 20;
                }
            }

            GameController.LeftPanel.StartDrawPoint = position;
        }

        //TODO Rewrite with use ImGuiRender.DrawMultiColoredText()
        private Vector2 DrawCounters(Vector2 position)
        {
            const int INNER_MARGIN = 15;
            position.Y += 5;
            var drawText = Graphics.DrawText(counters[MonsterRarity.White].ToString(), position, Color.White,
                FontAlign.Right);
            position.X -= INNER_MARGIN + drawText.X;
            drawText = Graphics.DrawText(counters[MonsterRarity.Magic].ToString(), position, HudSkin.MagicColor,
                FontAlign.Right);
            position.X -= INNER_MARGIN + drawText.X;
            drawText = Graphics.DrawText(counters[MonsterRarity.Rare].ToString(), position, HudSkin.RareColor,
                FontAlign.Right);
            position.X -= INNER_MARGIN + drawText.X;
            drawText = Graphics.DrawText(counters[MonsterRarity.Unique].ToString(), position, HudSkin.UniqueColor,
                FontAlign.Right);

            return drawText.TranslateToNum();
        }

        public override void EntityAdded(Entity Entity)
        {
        }

        private void Calc(Entity Entity)
        {
            var areaHash = GameController.Area.CurrentArea.Hash;

            if (!countedIds.TryGetValue(areaHash, out var monstersHashSet))
            {
                monstersHashSet = new HashSet<long>();
                countedIds[areaHash] = monstersHashSet;
            }

            if (!Entity.HasComponent<ObjectMagicProperties>()) return;
            var hashMonster = Entity.Id;

            if (!monstersHashSet.Contains(hashMonster))
            {
                monstersHashSet.Add(hashMonster);
                var rarity = Entity.Rarity;

                if (Entity.IsHostile && rarity >= MonsterRarity.White && rarity <= MonsterRarity.Unique &&
                    !string.IsNullOrEmpty(Entity.RenderName))
                {
                    counters[rarity]++;
                    summaryCounter++;
                }

                if (rarity != MonsterRarity.Unique)
                {
                    return;
                }

                if (generals.ContainsKey(Entity.Metadata))
                {
                    if (GameController.IngameState.IngameUi.Map.LargeMap.IsVisibleLocal)
                    {
                        var general = times[generals[Entity.Metadata]];
                        general.Time = DateTime.Now.AddSeconds(Settings.GeneralCooldown);
                        general.EntityZ = Entity.GetComponent<Render>().Z;
                        general.GridPos = Entity.GetComponent<Positioned>().GridPos;
                    }

                    bossCounters[generals[Entity.Metadata]]++;
                }
            }
        }

        private void DrawToLargeMiniMapText(BossInfo entity, MinimapTextInfo info)
        {
            var camera = GameController.Game.IngameState.Camera;
            var mapWindow = GameController.Game.IngameState.IngameUi.Map;
            if (GameController.Game.IngameState.UIRoot.Scale == 0)
            {
                DebugWindow.LogError(
                    "ExpeditionIcons: Seems like UIRoot.Scale is 0. Icons will not be drawn because of that.");
            }

            var mapRect = mapWindow.GetClientRect();
            var playerPos = GameController.Player.GetComponent<Positioned>().GridPos;
            var posZ = GameController.Player.GetComponent<Render>().Z;
            var screenCenter = new Vector2(mapRect.Width / 2, mapRect.Height / 2).Translate(0, -20) +
                               new Vector2(mapRect.X, mapRect.Y) +
                               new Vector2(mapWindow.LargeMapShiftX, mapWindow.LargeMapShiftY);
            var diag = (float) Math.Sqrt(camera.Width * camera.Width + camera.Height * camera.Height);
            var k = camera.Width < 1024f ? 1120f : 1024f;
            var scale = k / camera.Height * camera.Width * 3f / 4f / mapWindow.LargeMapZoom;
            var iconZ = entity.EntityZ;
            var point = screenCenter + DeltaInWorldToMinimapDelta(entity.GridPos - playerPos, diag, scale,
                (iconZ - posZ) / (9f / mapWindow.LargeMapZoom));
            var seconds = (entity.Time - DateTime.Now).TotalSeconds;
            if (seconds < 0)
            {
                //return;
                seconds = 0;
            }

            info.FontBackgroundColor = seconds > 0 ? Color.Red : Color.Green;
            var size = Graphics.DrawText(WordWrap(seconds.ToString("F1"), info.TextWrapLength), point, info.FontColor,
                info.FontSize, FontAlign.Center);
            float maxWidth = 0;
            float maxheight = 0;
            //not sure about sizes below, need test
            point.Y += size.Y;
            maxheight += size.Y;
            maxWidth = Math.Max(maxWidth, size.X);
            var background = new RectangleF(point.X - maxWidth / 2 - 3, point.Y - maxheight, maxWidth + 6, maxheight);
            Graphics.DrawBox(background, info.FontBackgroundColor);
        }

        public static Vector2 DeltaInWorldToMinimapDelta(Vector2 delta, double diag, float scale, float deltaZ = 0)
        {
            const float CAMERA_ANGLE = 38 * MathUtil.Pi / 180;

            // Values according to 40 degree rotation of cartesian coordiantes, still doesn't seem right but closer
            var cos = (float) (diag * Math.Cos(CAMERA_ANGLE) / scale);
            var sin = (float) (diag * Math.Sin(CAMERA_ANGLE) /
                               scale); // possible to use cos so angle = nearly 45 degrees

            // 2D rotation formulas not correct, but it's what appears to work?
            return new Vector2((delta.X - delta.Y) * cos, deltaZ - (delta.X + delta.Y) * sin);
        }

        public static string WordWrap(string input, int maxCharacters)
        {
            var lines = new List<string>();
            if (!input.Contains(" "))
            {
                var start = 0;
                while (start < input.Length)
                {
                    lines.Add(input.Substring(start, Math.Min(maxCharacters, input.Length - start)));
                    start += maxCharacters;
                }
            }
            else
            {
                var words = input.Split(' ');
                var line = "";
                foreach (var word in words)
                {
                    if ((line + word).Length > maxCharacters)
                    {
                        lines.Add(line.Trim());
                        line = "";
                    }

                    line += string.Format("{0} ", word);
                }

                if (line.Length > 0)
                {
                    lines.Add(line.Trim());
                }
            }

            var conectedLines = "";
            foreach (var line in lines)
            {
                conectedLines += line + "\n\r";
            }

            return conectedLines;
        }
    }

    public class MinimapTextInfo
    {
        public MinimapTextInfo()
        {
        }

        public MinimapTextInfo(int fontSize, Color fontColor, Color fontBackgroundColor, int textWrapLength,
            int textOffsetY)
        {
            FontSize = fontSize;
            FontColor = fontColor;
            FontBackgroundColor = fontBackgroundColor;
            TextWrapLength = textWrapLength;
            TextOffsetY = textOffsetY;
        }

        public int FontSize { get; set; }
        public Color FontColor { get; set; }
        public Color FontBackgroundColor { get; set; }
        public int TextWrapLength { get; set; }
        public int TextOffsetY { get; set; }
    }

    public class KillCounterSettings : ISettings
    {
        public KillCounterSettings()
        {
            ShowDetail = new ToggleNode(true);
            ShowInTown = new ToggleNode(false);
            TextColor = new ColorBGRA(220, 190, 130, 255);
            BackgroundColor = new ColorBGRA(0, 0, 0, 255);
            LabelTextSize = new RangeNode<int>(16, 10, 20);
            KillsTextSize = new RangeNode<int>(16, 10, 20);
        }

        public ToggleNode ShowInTown { get; set; }
        public ToggleNode ShowDetail { get; set; }
        public ColorNode TextColor { get; set; }
        public ColorNode BackgroundColor { get; set; }
        public RangeNode<int> LabelTextSize { get; set; }
        public RangeNode<int> KillsTextSize { get; set; }
        public ToggleNode UseImguiForDraw { get; set; } = new ToggleNode(true);
        public ToggleNode MultiThreading { get; set; } = new ToggleNode(false);
        public ToggleNode Enable { get; set; } = new ToggleNode(false);
        public RangeNode<int> TimePosition { get; set; } = new RangeNode<int>(100, -500, 500);
        public RangeNode<int> GeneralCooldown { get; set; } = new RangeNode<int>(24, 0, 60);
        public ToggleNode DrawTime { get; set; } = new ToggleNode(false);
        public TextNode LegionFile { get; set; } = new TextNode("legioninfo.txt");
    }
}