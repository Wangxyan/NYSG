using System;
using System.Collections.Generic;
using UnityEngine; // 需要 UnityEngine 来使用 Mathf

[Serializable]
public class JsonItemData
{
    public int Id;
    public string Name;
    public string Description;
    public int charm;
    public int knowledge;
    public int talent;
    public int wealth;
    public string Res; // 用于加载 Sprite 的资源路径/名称
    public int weaponGroupNum;
    public int itemType; // 1为合法
    public string Type; // "H_W" 格式, 例如 "2_1" 表示高2宽1
    public int Level;
    public int SkillId;
    public List<List<int>> Points; // 保持定义，但初始重构主要用 Type 字段获取宽高
    public int options_num;
    public int Rarity;         // New field for item rarity, 0 = most common

    // New fields for item-specific audio paths
    public string SelectAudioPath; // e.g., "ItemAudios/item_select_sound_name"
    public string PlaceAudioPath;  // e.g., "ItemAudios/item_place_sound_name"

    private int parsedWidth = -1;
    private int parsedHeight = -1;

    public int ParsedWidth
    {
        get
        {
            if (parsedWidth == -1) ParseType();
            return parsedWidth;
        }
    }

    public int ParsedHeight
    {
        get
        {
            if (parsedHeight == -1) ParseType();
            return parsedHeight;
        }
    }

    private void ParseType()
    {
        if (string.IsNullOrEmpty(Type))
        {
            Debug.LogWarning($"Item ID {Id} ('{Name}') has null or empty 'Type' field. Defaulting to 1x1.");
            parsedHeight = 1;
            parsedWidth = 1;
            return;
        }

        string[] dimensions = Type.Split('_');
        if (dimensions.Length == 2)
        {
            if (int.TryParse(dimensions[0], out int h) && int.TryParse(dimensions[1], out int w))
            {
                parsedHeight = h;
                parsedWidth = w;
            }
            else
            {
                Debug.LogWarning($"Item ID {Id} ('{Name}') has invalid 'Type' field format: {Type}. Expected H_W. Defaulting to 1x1.");
                parsedHeight = 1;
                parsedWidth = 1;
            }
        }
        else
        {
            Debug.LogWarning($"Item ID {Id} ('{Name}') has invalid 'Type' field format: {Type}. Expected H_W. Defaulting to 1x1.");
            parsedHeight = 1;
            parsedWidth = 1;
        }
    }

    // 从 Points 计算包围盒宽度 (可选，如果Type字段足够)
    public int WidthFromPoints
    {
        get
        {
            if (Points == null || Points.Count == 0) return 0;
            int maxWidth = 0;
            foreach (List<int> row in Points)
            {
                if (row != null && row.Count > 0)
                {
                    // 假设内部列表的最后一个元素代表该行的最大列索引 (如果元素是1代表占据)
                    // 或者如果内部列表存储的是占据的列号，则取最大值
                    // 为了简单，我们假设内部列表存储的是已占据的列的索引，我们需要找到这些索引中的最大值+1
                    // 或者，如果内部列表的长度代表了该行的宽度（从0开始）
                    // 根据你对 Points 的描述："内部列表的元素代表该行中的哪些列被占据"
                    // 如果 Points[row][col_index] 存在，表示 (row, col_index) 被占据
                    // 我们需要找到所有被占据的列的最大索引值
                    int currentRowMaxWidth = 0;
                    foreach(int colOccupation in row) // 假设 colOccupation 是 0 或 1
                    {
                        // 如果内部列表代表的是占据的列号，例如 [0, 2, 3]
                        // if (col > currentRowMaxWidth) currentRowMaxWidth = col;
                        // 如果内部列表是长度，而元素是标志位，例如 [1,0,1,1]
                        // currentRowMaxWidth = row.Count;
                        // 根据你的描述 "内部列表（或其内容）代表该行中的哪些列被占据"
                        // 如果是 [1,1,1]，宽度是3。如果是 [0,1,0]，宽度可能是3，但第一个元素是0.
                        // 更清晰的方式是，Points 的每个内部 List<int> 的 Count 就是该行的最大列数（如果从0开始计数）
                        // 或者内部 List<int> 存储的是被占据的列的索引，例如 [[0,1],[0]] 表示一个L形状
                        // 假设内部 List<int> 包含实际的列索引
                         if(row.Count > 0) {
                            int maxColInRow = 0;
                            foreach(int c in row) if(c > maxColInRow) maxColInRow = c;
                            if ((maxColInRow + 1) > currentRowMaxWidth) currentRowMaxWidth = (maxColInRow + 1);
                         }
                    }
                    if (currentRowMaxWidth > maxWidth) maxWidth = currentRowMaxWidth;
                }
            }
            return maxWidth;
        }
    }

    // 从 Points 计算包围盒高度 (可选)
    public int HeightFromPoints
    {
        get
        {
            if (Points == null) return 0;
            return Points.Count; // 外部列表的元素数量代表行数
        }
    }
}

// 如果JSON文件是一个包含 JsonItemData 对象数组的根对象，我们可能需要一个包装类
[Serializable]
public class JsonItemDataCollection
{
    public List<JsonItemData> items;
} 