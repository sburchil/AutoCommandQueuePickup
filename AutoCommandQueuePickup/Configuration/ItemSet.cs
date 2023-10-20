using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using RoR2;

namespace AutoCommandQueuePickup.Configuration;

public class ItemSet
{
    public enum ItemNameSource
    {
        EN_Name,
        Def_Name
    }

    public static readonly Dictionary<string, ItemWrapper> ItemsByEnglishName = new();

    public static readonly Dictionary<string, ItemWrapper> ItemsByDefName = new();

    public static readonly Dictionary<ItemIndex, ItemWrapper> ItemsByIndex = new();

    private static readonly Regex regexEscapes =
        new(@"(?:([^\\]+)|(?:\\(.)))", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex regexEntries = new(@"([^,]*)(,|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly HashSet<ItemWrapper> items = new();

    public IList<string> ParseErrors;

    static ItemSet()
    {
        TryInitialize();
        ItemCatalog.availability.CallWhenAvailable(() => TryInitialize());
        RoR2Application.onLoad += () => TryInitialize();
    }

    public ItemSet()
    {
        if (!Initialized)
            TryInitialize();
    }

    public static bool Initialized { get; private set; }

    public static string Escape(string s)
    {
        return s.Replace(",", "\\,");
    }

    public void AddParseError(string error)
    {
        if (ParseErrors == null) ParseErrors = new List<string>();
        ParseErrors.Add(error);
    }

    public static IEnumerable<string> Parse(string s)
    {
        var matches = regexEscapes.Matches(s);

        var entries = new List<string>();

        var sb = new StringBuilder();
        foreach (Match match in matches)
            if (match.Groups[2].Length > 0)
                sb.Append(match.Groups[2].Value);
            else
                foreach (Match submatch in regexEntries.Matches(match.Groups[1].Value))
                {
                    sb.Append(submatch.Groups[1].Value);

                    if (submatch.Groups[2].Length > 0)
                    {
                        entries.Add(sb.ToString());
                        sb.Clear();
                    }
                }

        if (sb.Length > 0) entries.Add(sb.ToString());

        return entries.Select(entry => entry.Trim());
    }

    public static string Serialize(ItemSet self)
    {
        if (!TryInitialize()) throw new InvalidOperationException("ItemSet cannot be initialized at this point");

        var sb = new StringBuilder();

        using (var enumerator = self.items.Select(item => Escape(item.GetName())).GetEnumerator())
        {
            if (enumerator.MoveNext())
            {
                sb.Append(enumerator.Current);
                while (enumerator.MoveNext())
                {
                    sb.Append(", ");
                    sb.Append(enumerator.Current);
                }
            }
        }

        return sb.ToString();
    }

    public static ItemSet Deserialize(string src)
    {
        if (!TryInitialize()) throw new InvalidOperationException("ItemSet cannot be initialized at this point");

        var result = new ItemSet();
        var entries = Parse(src);

        foreach (var itemString in entries)
        {
            ItemWrapper item;

            if (!ItemsByEnglishName.TryGetValue(itemString, out item) &&
                !ItemsByDefName.TryGetValue(itemString, out item))
            {
                result.AddParseError($"Invalid item in blacklist: {itemString}; ignoring");

                item = new ItemWrapper
                {
                    itemIndex = ItemIndex.None,
                    EN_Name = itemString,
                    itemNameSource = ItemNameSource.EN_Name
                }; // Should save the invalid items back into config in case that ever happens
            }

            result.items.Add(item);
        }


        return result;
    }

    public override string ToString()
    {
        return Serialize(this);
    }

    internal static event Action OnInitialized;

    public static bool TryInitialize()
    {
        if (Initialized) return true;
        if (!ItemCatalog.availability.available || Language.english == null) return false;

        var language = Language.english;
        foreach (var def in ItemCatalog.allItems.Select(ItemCatalog.GetItemDef))
        {
            var index = def.itemIndex;
            string en_name = null;
            if (def.nameToken != null)
                en_name = language.GetLocalizedStringByToken(def.nameToken);

            var wrapper = new ItemWrapper
            {
                itemIndex = index,
                EN_Name = en_name,
                Def_Name = def.name,
                itemNameSource = ItemNameSource.EN_Name
            };

            ItemsByIndex[index] = wrapper;
            if (en_name != null)
                ItemsByEnglishName[en_name] = wrapper;
            wrapper.itemNameSource = ItemNameSource.Def_Name;
            ItemsByDefName[def.name] = wrapper;
        }

        foreach (var wrapper in ItemsByIndex.Values.Where(wrapper => wrapper.EN_Name != null))
        {
            var trimmedName = wrapper.EN_Name.Trim();
            if (!ItemsByEnglishName.ContainsKey(trimmedName))
                ItemsByEnglishName[trimmedName] = wrapper; // Just in case
        }

        Initialized = true;
        OnInitialized?.Invoke();
        return true;
    }

    public void AddItem(ItemWrapper item)
    {
        items.Add(item);
    }

    public void RemoveItem(ItemWrapper item)
    {
        items.Remove(item);
    }

    public bool HasItem(ItemWrapper item)
    {
        return items.Contains(item);
    }

    public void AddItem(ItemWrapper? item)
    {
        AddItem(item.Value);
    }

    public void RemoveItem(ItemWrapper? item)
    {
        RemoveItem(item.Value);
    }

    public bool HasItem(ItemWrapper? item)
    {
        return HasItem(item.Value);
    }

    public void AddItem(string name)
    {
        AddItem(GetItemByName(name));
    }

    public void RemoveItem(string name)
    {
        RemoveItem(GetItemByName(name));
    }

    public bool HasItem(string name)
    {
        return HasItem(GetItemByName(name));
    }

    public void AddItem(ItemIndex name)
    {
        AddItem(GetItemByIndex(name));
    }

    public void RemoveItem(ItemIndex name)
    {
        RemoveItem(GetItemByIndex(name));
    }

    public bool HasItem(ItemIndex name)
    {
        return HasItem(GetItemByIndex(name));
    }

    public static ItemWrapper? GetItemByName(string name)
    {
        ItemWrapper item;
        if (!ItemsByDefName.TryGetValue(name, out item)
            && !ItemsByEnglishName.TryGetValue(name, out item)
            && !ItemsByEnglishName.TryGetValue(name.Trim(), out item))
            return null;

        return item;
    }

    public static ItemWrapper? GetItemByIndex(ItemIndex index)
    {
        ItemWrapper item;
        if (!ItemsByIndex.TryGetValue(index, out item)) return null;

        return item;
    }

    public struct ItemWrapper
    {
        public ItemIndex itemIndex;
        public string EN_Name;
        public string Def_Name;

        public ItemNameSource itemNameSource;

        public string GetName()
        {
            switch (itemNameSource)
            {
                case ItemNameSource.EN_Name: return EN_Name;
                case ItemNameSource.Def_Name: return Def_Name;
                default: return EN_Name ?? Def_Name ?? itemIndex.ToString();
            }
        }

        public override bool Equals(object obj)
        {
            if (!(obj is ItemWrapper itemWrapper)) return false;

            return itemIndex == itemWrapper.itemIndex &&
                   EN_Name == itemWrapper.EN_Name;
        }

        public override int GetHashCode()
        {
            var hashCode = 17;
            hashCode = hashCode * 31 + itemIndex.GetHashCode();
            hashCode = hashCode * 31 + EqualityComparer<string>.Default.GetHashCode(EN_Name);
            hashCode = hashCode * 31 + EqualityComparer<string>.Default.GetHashCode(Def_Name);
            return hashCode;
        }
    }
}