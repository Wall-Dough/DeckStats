using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace DeckStats.DeckStatsCode;

//You're recommended but not required to keep all your code in this package and all your assets in the DeckStats folder.
[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "DeckStats"; //At the moment, this is used only for the Logger and harmony names.

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } =
        new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Harmony harmony = new(ModId);

        harmony.PatchAll();
        ModConfigBridge.DeferredRegister();
    }

    [HarmonyPatch]
    public class DeckStatsPatch
    {
        private static string _containerName = "DeckStats";
        private static string _labelName = "DeckStatsLabel";
        private static string _controlsName = "DeckStatsControls";
        private static string _secondCycleToggleName = "DeckStatsSecondCycleToggle";
        private static PileType? _lastPileType;
        private static Vector2? _cardSize;
        private static Font? _regularFont;
        private static Font? _boldFont;
        private static int _regularFontSize = 0;
        private static int _boldFontSize = 0;
        public static bool ShouldShowLogButton = false;

        private static void ShowLogButton(Control? viewScreen, Control? container)
        {
            if (container == null)
            {
                if (viewScreen == null)
                {
                    Logger.Error("Cannot show log button, both deck view screen and container are null");
                    return;
                }
                container = viewScreen.GetNode<Control>(_containerName);
                if (container == null)
                {
                    Logger.Warn("Container not found, creating simple container to add log button to");
                    container = new PanelContainer();
                    viewScreen.AddChild(container);
                    Control bottomText = viewScreen.GetNode<Control>("BottomText");
                    Vector2 position = bottomText.GetPosition(); 
                    Vector2 size = bottomText.GetSize();
                    container.SetPosition(new Vector2(position.X + size.X, position.Y));
                }
            }
            Button logButton = new Button();
            logButton.Text = "Get logs";
            logButton.SetHSizeFlags(Control.SizeFlags.ShrinkEnd);
            logButton.SetVSizeFlags(Control.SizeFlags.ShrinkEnd);
            logButton.Pressed += () =>
            {
                new GetLogsConsoleCmd().Process(null, []);
            };
            if (container.HasNode(_controlsName))
            {
                container = container.GetNode<Control>(_controlsName);
            }
            container.AddChild(logButton);
            container.MoveChild(logButton, 0);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NDeckViewScreen))]
        [HarmonyPatch("DisplayCards")]
        private static void BeforeDisplayCards(NDeckViewScreen __instance)
        {
            if (__instance.HasNode(new NodePath(_containerName)))
            {
                Control deckStatsNode = __instance.GetNode<Control>("DeckStatsNode");
                __instance.RemoveChild(deckStatsNode);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NCardGrid))]
        [HarmonyPatch("GetContainedCardsSize")]
        private static void AfterGetContainedCardsSize(ref Vector2 __result)
        {
            _cardSize = __result;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NCardGrid))]
        [HarmonyPatch("SetCards")]
        private static void BeforeSetCards(IReadOnlyList<CardModel> cardsToDisplay, PileType pileType,
            List<SortingOrders> sortingPriority, Task? taskToWaitOn)
        {
            ShouldShowLogButton = false;
            _lastPileType = pileType;
            
            Logger.Info($"Should be getting deck stats for pile type \"{pileType.ToString()}\"");
            
            DeckStats.CalculateDeckStats(pileType, cardsToDisplay);
        }

        private static Control CreateDeckStatsNode(Control viewScreen)
        {
            PanelContainer container = new PanelContainer();
            container.SetName(_containerName);
            StyleBox panelStyleBox = (StyleBox) container.GetThemeStylebox(new StringName("panel")).Duplicate();
            panelStyleBox.Set(new StringName("bg_color"), new Color(Colors.Black, 0.75f));
            container.AddThemeStyleboxOverride(new StringName("panel"), panelStyleBox);
            RichTextLabel label = new RichTextLabel();
            label.SetName(_labelName);
            label.SetFitContent(true);
            if (_regularFont != null && _boldFont != null)
            {
                label.AddThemeFontOverride(ThemeConstants.RichTextLabel.NormalFont, _regularFont);
                label.AddThemeFontOverride(ThemeConstants.RichTextLabel.BoldFont, _boldFont);
                label.AddThemeColorOverride(ThemeConstants.RichTextLabel.FontShadowColor, Colors.Black);
                label.AddThemeFontSizeOverride(ThemeConstants.RichTextLabel.NormalFontSize, _regularFontSize);
                label.AddThemeFontSizeOverride(ThemeConstants.RichTextLabel.BoldFontSize, _boldFontSize);
            }
            else
            {
                Logger.Warn("Could not find font files");
                ShouldShowLogButton = true;
            }
            label.SetAutowrapMode(TextServer.AutowrapMode.Off);
            if (_lastPileType != null)
            {
                label.Visible = DeckStats.IsDeckStatsToggled((PileType) _lastPileType);
            }
            else
            {
                Logger.Error("Null last pile type");
                ShouldShowLogButton = true;
            }
            VBoxContainer vContainer = new();
            Button toggleButton = new();
            toggleButton.Text = "Deck Stats";
            toggleButton.Pressed += () =>
            {
                label.Visible = !label.Visible;
                if (_lastPileType != null)
                {
                    DeckStats.SetDeckStatsToggled((PileType) _lastPileType, label.Visible);
                }
                else
                {
                    Logger.Error("Null last pile type");
                    ShouldShowLogButton = true;
                }
                ResetBottomTextPosition(viewScreen);
                UpdateDeckStatsPosition(viewScreen);
            };
            vContainer.AddChildSafely(toggleButton);
            vContainer.AddChildSafely(label);
            container.AddChildSafely(vContainer);
            HBoxContainer hBoxContainer = new();
            hBoxContainer.SetName(_controlsName);
            CheckBox secondCycleCheckbox = new();
            secondCycleCheckbox.SetName(_secondCycleToggleName);
            secondCycleCheckbox.SetText("Second cycle");
            if (_lastPileType != null)
            {
                secondCycleCheckbox.SetPressed(DeckStats.IsSecondCycleToggled((PileType)_lastPileType));
            }
            else
            {
                Logger.Error("Null last pile type");
                ShouldShowLogButton = true;
            }
            secondCycleCheckbox.Pressed += () =>
            {
                if (_lastPileType != null)
                {
                    DeckStats.SetSecondCycleToggled((PileType) _lastPileType, secondCycleCheckbox.IsPressed());
                }
                else
                {
                    Logger.Error("Null last pile type");
                    ShouldShowLogButton = true;
                }
            };
            hBoxContainer.AddChildSafely(secondCycleCheckbox);
            hBoxContainer.SetHSizeFlags(Control.SizeFlags.ShrinkEnd);
            hBoxContainer.SetVSizeFlags(Control.SizeFlags.ShrinkEnd);
            container.AddChildSafely(hBoxContainer);
            return container;
        }

        private static void PopulateDeckStatsLabel(Control container)
        {
            RichTextLabel label;
            if (container.HasNode(new NodePath(_labelName)))
            {
                label = container.GetNode<RichTextLabel>(_labelName);
            }
            else
            {
                label = container.GetChild(0).GetNode<RichTextLabel>(_labelName);
            }
            if (label == null)
            {
                Logger.Error("No deck stats label found");
                ShouldShowLogButton = true;
                return;
            }
            if (_lastPileType == null)
            {
                Logger.Error("Null last pile type");
                ShouldShowLogButton = true;
                return;
            }
            PileType pileType = (PileType) _lastPileType;

            DeckStats.LoadConfig();

            label.Clear();

            int tableWidth = DeckStats.GetStatTableWidth();
            int tableHeight = DeckStats.GetStatTableHeight();
            label.PushTable(tableWidth * 3);
            Rect2 cellPadding = new Rect2(10, 0, 10, 0);
            int totalCards = DeckStats.GetTotalCardCount(pileType);
            for (int rowNum = 0; rowNum < tableHeight; rowNum++)
            {
                for (int colNum = 0; colNum < tableWidth; colNum++)
                {
                    string statName = DeckStats.GetStatTableCell(rowNum, colNum);
                    int statValue = DeckStats.GetStatValue(pileType, statName);
                    if (statName == DeckStats.NONE || statValue < 0)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            label.PushCell();
                            label.SetCellPadding(cellPadding);
                            label.Pop();
                        }
                        continue;
                    }
                    int percent = (int) ((float) statValue / totalCards * 100);
                    label.PushCell();
                    label.SetCellPadding(cellPadding);
                    label.AppendText("[b]" + statName.Replace('_', ' ') + ":[/b]");
                    label.Pop();
                    label.PushCell();
                    label.SetCellPadding(cellPadding);
                    label.AppendText(statValue.ToString());
                    label.Pop();
                    label.PushCell();
                    label.SetCellPadding(cellPadding);
                    label.AppendText("(" + percent + "%)");
                    label.Pop();
                }
            }
            label.Pop();
        }

        private static void LogAllChildren(Node parent)
        {
            Logger.Info("All child nodes of:" + parent.GetPath());
            foreach (Node child in parent.GetChildren())
            {
                Logger.Info("  " + child.GetName() + " (" + child.GetType().Name + ")");
            }
        }

        private static void ResetBottomTextPosition(Control viewScreen)
        {
            Control cardGrid = viewScreen.GetNode<Control>("CardGrid");
            Control bottomText = viewScreen.GetNode<Control>("BottomText");
            Vector2 bottomTextPosition = new Vector2(bottomText.GetPosition().X,
                cardGrid.GetPosition().Y + cardGrid.GetSize().Y - bottomText.GetSize().Y);
            bottomText.SetPosition(bottomTextPosition);
        }

        private static void UpdateDeckStatsPosition(Control viewScreen)
        {
            float containerWidth = 300;
            if (_cardSize == null)
            {
                Logger.Warn("Null DeckStats card size");
                ShouldShowLogButton = true;
            }
            else
            {
                containerWidth = _cardSize.Value.X;
            }
            Control cardGrid = viewScreen.GetNode<Control>("CardGrid");
            Control scrollContainer = cardGrid.GetNode<Control>("ScrollContainer");
            Control bottomText = viewScreen.GetNode<Control>("BottomText");
            Control container = GetContainerNode(viewScreen);
            RichTextLabel label = GetLabelNode(viewScreen);
            float padding = 100f;
            Control button = (Control)container.GetChild(0).GetChild(0);
            float containerHeight = button.GetSize().Y + 10;
            if (label.Visible)
            {
                containerHeight += label.GetContentHeight();
            }
            Vector2 position = new Vector2(scrollContainer.GetPosition().X + padding,
                cardGrid.GetPosition().Y + cardGrid.GetSize().Y - containerHeight);
            container.SetPosition(position);
            container.SetSize(new Vector2(containerWidth, containerHeight));
            Vector2 bottomTextPosition = new Vector2(bottomText.GetPosition().X, position.Y - bottomText.GetSize().Y - 10);
            bottomText.SetPosition(bottomTextPosition);
        }

        private static Control GetContainerNode(Control viewScreen)
        {
            Control container = viewScreen.GetNode<Control>(_containerName);
            return container;
        }

        private static RichTextLabel GetLabelNode(Control viewScreen)
        {
            Control container = GetContainerNode(viewScreen);
            RichTextLabel label;
            if (container.HasNode(new NodePath(_labelName)))
            {
                return container.GetNode<RichTextLabel>(_labelName);
            }
            else
            {
                return container.GetChild(0).GetNode<RichTextLabel>(_labelName);
            }
        }
        
        private static Control GetControlsNode(Control viewScreen)
        {
            Control container = GetContainerNode(viewScreen);
            return container.GetNode<Control>(_controlsName);
        }

        private static void ShowDeckStats(Control viewScreen)
        {
            ResetBottomTextPosition(viewScreen);
            if (_lastPileType != PileType.Deck && _lastPileType != PileType.Draw && _lastPileType != PileType.Discard)
            {
                return;
            }
            
            if (_regularFont == null || _boldFont == null)
            {
                Control bottomText = viewScreen.GetNode<Control>("BottomText");
                MegaRichTextLabel bottomLabel = bottomText.GetNode<MegaRichTextLabel>("MarginContainer/BottomLabel");
                _regularFont = bottomLabel.GetThemeFont(ThemeConstants.RichTextLabel.NormalFont);
                _boldFont = bottomLabel.GetThemeFont(ThemeConstants.RichTextLabel.BoldFont);
                _regularFontSize = bottomLabel.GetThemeFontSize(ThemeConstants.RichTextLabel.NormalFontSize);
                _boldFontSize = bottomLabel.GetThemeFontSize(ThemeConstants.RichTextLabel.BoldFontSize);
            }
            
            if (!viewScreen.HasNode(new NodePath(_containerName)))
            {
                Control container = CreateDeckStatsNode(viewScreen);
                if (container == null)
                {
                    Logger.Error("Null DeckStats container");
                    ShouldShowLogButton = true;
                    ShowLogButton(viewScreen, null);
                    return;
                }
                PopulateDeckStatsLabel(container);

                viewScreen.AddChild(container);
                
                UpdateDeckStatsPosition(viewScreen);

                if (ShouldShowLogButton)
                {
                    ShowLogButton(viewScreen, container);
                }
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(NDeckViewScreen))]
        [HarmonyPatch("DisplayCards")]
        private static void AfterDeckScreenDisplayCards(NDeckViewScreen __instance)
        {
            ShowDeckStats(__instance);
        }
        
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NCardPileScreen))]
        [HarmonyPatch("OnPileContentsChanged")]
        private static void AfterCardPileContentsChanged(NCardPileScreen __instance)
        {
            ShowDeckStats(__instance);
        }
    }
}