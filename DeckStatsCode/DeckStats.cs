using System.Collections;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace DeckStats.DeckStatsCode;

public static class DeckStats
{
    private static string DISCARD_PATTERN = "[Dd]iscard(?! Pile)(?!ed)";

    private static bool _configLoaded = false;
    public static string NONE = "(None)";
    public static string TOTAL = "Total";
    public static string ATTACKS = "Attacks";
    public static string SKILLS = "Skills";
    public static string POWERS = "Powers";
    public static string CURSES = "Curses";
    public static string QUESTS = "Quests";
    public static string SINGLE_TARGET = "Single_Target";
    public static string AOE = "AOE";
    public static string RANDOM_ENEMY = "Random_Enemy";
    public static string BLOCK = "Block";
    public static string WEAK = "Weak";
    public static string VULNERABLE = "Vulnerable";
    public static string CARD_DRAW = "Card_Draw";
    public static string ETHEREAL = "Ethereal";
    public static string DISCARD = "Discard";
    public static string SLY = "Sly";
    private static CharacterModel? _character = null;

    public static MegaCrit.Sts2.Core.Logging.Logger Logger = MainFile.Logger;

    public static string[] ALL_STATS =
    [
        NONE,
        TOTAL, ATTACKS, SKILLS, POWERS, CURSES, QUESTS,
        SINGLE_TARGET, AOE, RANDOM_ENEMY,
        BLOCK, WEAK, VULNERABLE, CARD_DRAW, ETHEREAL,
        DISCARD, SLY
    ];

    public static string[][] COLUMNS =
    [
        [TOTAL, ATTACKS,       SKILLS, POWERS,       CURSES,    QUESTS],
        [NONE,  SINGLE_TARGET, AOE,    RANDOM_ENEMY],
        [NONE,  BLOCK,         WEAK,   VULNERABLE,   CARD_DRAW]
    ];

    public static string[][] CHARACTER_COLUMNS =
    [
        [VULNERABLE], // The Ironclad
        [WEAK, DISCARD, SLY], // The Silent
        [WEAK, VULNERABLE], // The Regent (star gain, star spend)
        [ETHEREAL], // The Necrobinder
        [] // The Defect
    ];

    private static string[]? _characterColumn = null;

    public static string[][] currentColumns = COLUMNS;
    
    public static Hashtable showStats = new();
    public static Hashtable statValuesByPileType = new();
    private static Hashtable _deckStatsToggled = new();
    private static Hashtable _secondCycleToggled = new();

    public static void LoadConfig(bool forceLoad = false)
    {
        if (!ModConfigBridge.IsAvailable)
        {
            MainFile.Logger.Warn("ModConfigBridge unavailable, skipping");
            
            return;
        }
        if (_configLoaded && !forceLoad)
        {
            MainFile.Logger.Warn("Config already loaded, skipping");
            
            return;
        }
        showStats.Clear();
        foreach (string statName in ALL_STATS)
        {
            if (statName == NONE) continue;

            showStats[statName] = ModConfigBridge.GetValue(statName, true);
        }
        UpdateColumns();
    }

    public static void ShowStat(string statName, bool show)
    {
        showStats[statName] = show;
    }

    public static bool GetShowStat(string statName)
    {
        return !showStats.ContainsKey(statName) || showStats[statName] is true;
    }

    public static void SetCharacter(CharacterModel? character)
    {
        if (_character == character)
        {
            return;
        }
        if (character == null)
        {
            Logger.Info("Set null character");
        }
        else
        {
            Logger.Info($"Setting character {character.Title.GetRawText()}");
        }
        _character = character;
        _characterColumn = null;
        if (_character == null)
        {
            return;
        }
        if (_character is Ironclad)
        {
            _characterColumn = CHARACTER_COLUMNS[0];
        }
        else if (_character is Silent)
        {
            _characterColumn = CHARACTER_COLUMNS[1];
        }
        else if (_character is Regent)
        {
            _characterColumn = CHARACTER_COLUMNS[2];
        }
        else if (_character is Necrobinder)
        {
            _characterColumn = CHARACTER_COLUMNS[3];
        }
        else if (_character is Defect)
        {
            _characterColumn = CHARACTER_COLUMNS[4];
        }

        if (_characterColumn == null)
        {
            Logger.Info("Null character column");
        }
        else
        {
            Logger.Info($"Character column: {string.Join(", ", _characterColumn)}");
        }
    }

    public static string[]? GetCharacterColumn()
    {
        if (_character == null)
        {
            return null;
        }
        return _characterColumn;
    }

    public static void UpdateColumns()
    {
        string[][] newColumns = new string[COLUMNS.GetUpperBound(0) + 1][];

        int numColumns = 0;
        foreach (string[] column in COLUMNS)
        {
            string[] newColumn = new string[column.GetUpperBound(0) + 1];
            int numRows = 0;
            foreach (string statName in column)
            {
                if (GetShowStat(statName))
                {
                    newColumn[numRows++] = statName;
                }
            }
            newColumns[numColumns++] = newColumn;
        }
        
        currentColumns = newColumns;
    }

    public static string GetCharacterColumnCell(int rowNum)
    {
        if (_character == null)
        {
            return NONE;
        }
        if (_characterColumn == null || _characterColumn.GetUpperBound(0) == 0)
        {
            return NONE;
        }
        if (rowNum == 0)
        {
            return _character.Title.GetRawText();
        }
        rowNum -= 1;
        if (rowNum > _characterColumn.GetUpperBound(0))
        {
            return NONE;
        }
        return _characterColumn[rowNum];
    }

    public static string GetStatTableCell(int rowNum, int colNum)
    {
        if (colNum < currentColumns.GetLowerBound(0))
        {
            return NONE;
        }
        if (colNum > currentColumns.GetUpperBound(0))
        {
            return GetCharacterColumnCell(rowNum);
        }
        string[] column = currentColumns[colNum];
        if (rowNum < column.GetLowerBound(0) || rowNum > column.GetUpperBound(0))
        {
            return NONE;
        }
        return column[rowNum];
    }

    public static int[] GetStatArray(PileType pileType, string statName)
    {
        Hashtable statValues = GetStatValuesForPile(pileType);
        if (statValues.ContainsKey(statName))
        {
            object? value = statValues[statName];
            if (value != null && value is int[])
            {
                return (int[]) value;
            }
        }
        return [];
    }
    
    public static int GetStatValue(PileType pileType, string statName)
    {
        int[] statArray = GetStatArray(pileType, statName);
        if (statArray.Length == 0)
        {
            return -1;
        }

        int idx = 0;
        if (IsSecondCycleToggled(pileType) && statArray.Length > 1)
        {
            idx = 1;
        }

        return statArray[idx];
    }

    public static int GetStatTableWidth()
    {
        string[]? characterColumn = GetCharacterColumn();
        int numCharacterColumns = characterColumn != null ? 1 : 0;
        return currentColumns.GetUpperBound(0) + numCharacterColumns + 1;
    }

    public static int GetStatTableHeight()
    {
        if (currentColumns == null)
        {
            MainFile.Logger.Warn("null current columns");
            return 0;
        }
        int maxLength = 0;
        foreach (string[] column in currentColumns)
        {
            if (column == null)
            {
                MainFile.Logger.Warn("null column");
                continue;
            }
            if (column.Length > maxLength)
            {
                maxLength = column.Length;
            }
        }
        if (_characterColumn != null && _characterColumn.Length + 1 > maxLength)
        {
            maxLength = _characterColumn.Length + 1;
        }
        return maxLength;
    }

    private static Hashtable GetStatValuesForPile(PileType pileType)
    {
        if (statValuesByPileType.ContainsKey(pileType))
        {
            return (Hashtable) statValuesByPileType[pileType];
        }
        Hashtable statValues = new();
        statValuesByPileType[pileType] = statValues;
        return statValues;
    }

    public static bool IsDeckStatsToggled(PileType pileType)
    {
        if (_deckStatsToggled.ContainsKey(pileType))
        {
            return (bool) _deckStatsToggled[pileType];
        }

        bool toggled = false;
        if (pileType == PileType.Deck || pileType == PileType.Draw)
        {
            toggled = true;
        }

        _deckStatsToggled[pileType] = toggled;

        return toggled;
    }

    public static bool IsSecondCycleToggled(PileType pileType)
    {
        if (!_secondCycleToggled.ContainsKey(pileType))
        {
            _secondCycleToggled[pileType] = false;
        }
        return (bool) _secondCycleToggled[pileType];
    }

    public static void SetSecondCycleToggled(PileType pileType, bool toggled)
    {
        _secondCycleToggled[pileType] = toggled;
    }

    public static void SetDeckStatsToggled(PileType pileType, bool toggled)
    {
        _deckStatsToggled[pileType] = toggled;
    }

    public static void CalculateDeckStats(PileType pileType, IReadOnlyList<CardModel> cards)
    {
        Hashtable statValues = GetStatValuesForPile(pileType);

        statValues.Clear();
        if (cards.Count == 0)
        {
            return;
        }
        SetCharacter(cards.First().Owner.Character);
        int[] totalCards = [0, 0];
        int[] numAttacks = [0, 0];
        int[] numSingleTarget = [0, 0];
        int[] numAOE = [0, 0];
        int[] numRandom = [0, 0];
        int[] numSkills = [0, 0];
        int[] numPowers = [0, 0];
        int[] numCurses = [0, 0];
        int[] numQuests = [0, 0];
        int[] numBlock = [0, 0];
        int[] numWeak = [0, 0];
        int[] numVulnerable = [0, 0];
        int[] numCardDraw = [0, 0];
        int[] numEthereal = [0, 0];
        int[] numDiscard = [0, 0];
        int[] numSly = [0, 0];
        foreach (CardModel card in cards)
        {
            try
            {
                int secondCycleCount = 1;
                if (card.Keywords.Contains(CardKeyword.Ethereal) && card.Type == CardType.Curse)
                {
                    secondCycleCount = 0;
                }
                if (card.Keywords.Contains(CardKeyword.Exhaust))
                {
                    secondCycleCount = 0;
                }
                if (card.Type == CardType.Power)
                {
                    secondCycleCount = 0;
                }
                totalCards[0]++;
                totalCards[1] += secondCycleCount;
                if (card.Type == CardType.Attack)
                {
                    numAttacks[0]++;
                    numAttacks[1] += secondCycleCount;
                }

                // TODO: get more specific with these?
                if (card.TargetType == TargetType.AnyEnemy)
                {
                    numSingleTarget[0]++;
                    numSingleTarget[1] += secondCycleCount;
                }

                if (card.TargetType == TargetType.AllEnemies)
                {
                    numAOE[0]++;
                    numAOE[1] += secondCycleCount;
                }

                if (card.TargetType == TargetType.RandomEnemy)
                {
                    numRandom[0]++;
                    numRandom[1] += secondCycleCount;
                }

                if (card.Type == CardType.Skill)
                {
                    numSkills[0]++;
                    numSkills[1] += secondCycleCount;
                }

                if (card.Type == CardType.Power)
                {
                    numPowers[0]++;
                    numPowers[1] += secondCycleCount;
                }

                if (card.Type == CardType.Curse)
                {
                    numCurses[0]++;
                    numCurses[1] += secondCycleCount;
                }

                if (card.Type == CardType.Quest)
                {
                    numQuests[0]++;
                    numQuests[1] += secondCycleCount;
                }

                if (card.GainsBlock)
                {
                    numBlock[0]++;
                    numBlock[1] += secondCycleCount;
                }

                if (card.DynamicVars.ContainsKey("WeakPower"))
                {
                    numWeak[0]++;
                    numWeak[1] += secondCycleCount;
                }

                if (card.DynamicVars.ContainsKey("VulnerablePower"))
                {
                    numVulnerable[0]++;
                    numVulnerable[1] += secondCycleCount;
                }

                if (card.DynamicVars.ContainsKey("Cards") || card.DynamicVars.ContainsKey("DrawCardsNextTurnPower"))
                {
                    numCardDraw[0]++;
                    numCardDraw[1] += secondCycleCount;
                }

                if (card.Keywords.Contains(CardKeyword.Ethereal))
                {
                    numEthereal[0]++;
                    numEthereal[1] += secondCycleCount;
                }

                if (card.Keywords.Contains(CardKeyword.Sly))
                {
                    numSly[0]++;
                    numSly[1] += secondCycleCount;
                }

                string rawDescription = card.Description.GetRawText();
                if (Regex.IsMatch(rawDescription, DISCARD_PATTERN))
                {
                    numDiscard[0]++;
                    numDiscard[1] += secondCycleCount;
                }
            }
            catch (Exception e)
            {
                Logger.Error($"There was a problem collecting stats for {card.Title}:\n{e.Message}");
                MainFile.DeckStatsPatch.ShouldShowLogButton = true;
            }
        }

        statValues.Add(TOTAL, totalCards);
        statValues.Add(ATTACKS, numAttacks);
        statValues.Add(SKILLS, numSkills);
        statValues.Add(POWERS, numPowers);    
        statValues.Add(CURSES, numCurses);
        statValues.Add(QUESTS, numQuests);
        statValues.Add(SINGLE_TARGET, numSingleTarget);
        statValues.Add(AOE, numAOE);
        statValues.Add(RANDOM_ENEMY, numRandom);
        statValues.Add(BLOCK, numBlock);
        statValues.Add(WEAK, numWeak);
        statValues.Add(VULNERABLE, numVulnerable);
        statValues.Add(CARD_DRAW, numCardDraw);
        statValues.Add(ETHEREAL, numEthereal);
        statValues.Add(DISCARD, numDiscard);
        statValues.Add(SLY, numSly);
    }

    public static int GetTotalCardCount(PileType pileType)
    {
        return GetStatValue(pileType, TOTAL);
    }
}
