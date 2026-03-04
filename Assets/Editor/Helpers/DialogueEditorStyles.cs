using UnityEditor;
using UnityEngine;

/// <summary>
/// Shared GUIStyles, colors, and constants for the Karma dialogue editor toolkit.
/// Used by PropertyDrawers, DialogueEditorWindow, VariableStoreBrowser, and DialoguePreview.
///
/// Color scheme based on Figma mockup:
///   Empathetic = warm orange
///   Selfish = cool dark
///   Neutral = clean white
///   Conditions = teal badges
///   Actions = purple badges
/// </summary>
public static class DialogueEditorStyles
{
    // ─── Node Colors ─────────────────────────────────────────

    public static readonly Color StartNodeColor = new Color(0.2f, 0.8f, 0.4f, 1f);     // green
    public static readonly Color EndNodeColor = new Color(0.9f, 0.3f, 0.3f, 1f);        // red
    public static readonly Color ChoiceNodeColor = new Color(1f, 0.65f, 0.2f, 1f);       // orange
    public static readonly Color LinearNodeColor = new Color(0.4f, 0.6f, 0.9f, 1f);      // blue
    public static readonly Color OrphanNodeColor = new Color(1f, 0.9f, 0.3f, 1f);        // yellow
    public static readonly Color SkippedNodeColor = new Color(0.6f, 0.6f, 0.6f, 0.5f);   // gray (dimmed)

    // ─── Choice Style Colors ─────────────────────────────────

    public static readonly Color EmpatheticBg = new Color(1f, 0.65f, 0.2f, 0.3f);
    public static readonly Color SelfishBg = new Color(0.35f, 0.35f, 0.4f, 0.3f);
    public static readonly Color NeutralBg = new Color(0.85f, 0.85f, 0.85f, 0.3f);

    public static readonly Color EmpatheticBorder = new Color(1f, 0.55f, 0.1f, 1f);
    public static readonly Color SelfishBorder = new Color(0.5f, 0.4f, 0.5f, 1f);
    public static readonly Color NeutralBorder = new Color(0.7f, 0.7f, 0.7f, 1f);

    // ─── Tag Badge Colors ────────────────────────────────────

    public static readonly Color ConditionTagBg = new Color(0.15f, 0.65f, 0.65f, 0.25f); // teal
    public static readonly Color ConditionTagText = new Color(0.1f, 0.55f, 0.55f, 1f);
    public static readonly Color ActionTagBg = new Color(0.55f, 0.3f, 0.7f, 0.25f);       // purple
    public static readonly Color ActionTagText = new Color(0.5f, 0.25f, 0.65f, 1f);

    // ─── Misc Colors ─────────────────────────────────────────

    public static readonly Color CardBg = EditorGUIUtility.isProSkin
        ? new Color(0.22f, 0.22f, 0.22f, 1f)
        : new Color(0.92f, 0.92f, 0.92f, 1f);

    public static readonly Color CardBgHover = EditorGUIUtility.isProSkin
        ? new Color(0.26f, 0.26f, 0.26f, 1f)
        : new Color(0.88f, 0.88f, 0.88f, 1f);

    public static readonly Color SeparatorColor = EditorGUIUtility.isProSkin
        ? new Color(0.15f, 0.15f, 0.15f, 1f)
        : new Color(0.75f, 0.75f, 0.75f, 1f);

    public static readonly Color SelectedBorder = new Color(0.3f, 0.6f, 1f, 1f);

    // ─── GUIStyle Cache ──────────────────────────────────────

    private static GUIStyle _nodeCard;
    private static GUIStyle _nodeHeader;
    private static GUIStyle _tagBadge;
    private static GUIStyle _miniButton;
    private static GUIStyle _dialogueText;
    private static GUIStyle _sectionHeader;
    private static GUIStyle _previewBubble;

    /// <summary>Card-style box for dialogue nodes.</summary>
    public static GUIStyle NodeCard
    {
        get
        {
            if (_nodeCard == null)
            {
                _nodeCard = new GUIStyle("helpBox")
                {
                    padding = new RectOffset(8, 8, 6, 6),
                    margin = new RectOffset(2, 2, 2, 2)
                };
            }
            return _nodeCard;
        }
    }

    /// <summary>Bold header for node titles.</summary>
    public static GUIStyle NodeHeader
    {
        get
        {
            if (_nodeHeader == null)
            {
                _nodeHeader = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 12,
                    padding = new RectOffset(0, 0, 0, 2)
                };
            }
            return _nodeHeader;
        }
    }

    /// <summary>Small tag badge style for conditions/actions.</summary>
    public static GUIStyle TagBadge
    {
        get
        {
            if (_tagBadge == null)
            {
                _tagBadge = new GUIStyle(EditorStyles.miniLabel)
                {
                    padding = new RectOffset(6, 6, 2, 2),
                    margin = new RectOffset(2, 2, 1, 1),
                    fontSize = 10,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            return _tagBadge;
        }
    }

    /// <summary>Compact mini button for [+] [x] controls.</summary>
    public static GUIStyle MiniButton
    {
        get
        {
            if (_miniButton == null)
            {
                _miniButton = new GUIStyle(EditorStyles.miniButton)
                {
                    padding = new RectOffset(4, 4, 1, 1),
                    margin = new RectOffset(1, 1, 1, 1),
                    fixedWidth = 20,
                    fixedHeight = 18
                };
            }
            return _miniButton;
        }
    }

    /// <summary>Styled text area for dialogue content.</summary>
    public static GUIStyle DialogueText
    {
        get
        {
            if (_dialogueText == null)
            {
                _dialogueText = new GUIStyle(EditorStyles.textArea)
                {
                    wordWrap = true,
                    fontSize = 12,
                    padding = new RectOffset(6, 6, 4, 4)
                };
            }
            return _dialogueText;
        }
    }

    /// <summary>Section header style (Conditions, Actions, Choices).</summary>
    public static GUIStyle SectionHeader
    {
        get
        {
            if (_sectionHeader == null)
            {
                _sectionHeader = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(0, 0, 4, 2)
                };
                _sectionHeader.normal.textColor = EditorGUIUtility.isProSkin
                    ? new Color(0.75f, 0.75f, 0.75f, 1f)
                    : new Color(0.35f, 0.35f, 0.35f, 1f);
            }
            return _sectionHeader;
        }
    }

    /// <summary>Speech-bubble style for dialogue preview.</summary>
    public static GUIStyle PreviewBubble
    {
        get
        {
            if (_previewBubble == null)
            {
                _previewBubble = new GUIStyle("helpBox")
                {
                    fontSize = 13,
                    wordWrap = true,
                    richText = true,
                    padding = new RectOffset(12, 12, 8, 8),
                    margin = new RectOffset(4, 4, 4, 4)
                };
            }
            return _previewBubble;
        }
    }

    // ─── Drawing Helpers ─────────────────────────────────────

    /// <summary>Draw a horizontal separator line.</summary>
    public static void DrawSeparator()
    {
        GUILayout.Space(2);
        var rect = GUILayoutUtility.GetRect(1, 1, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, SeparatorColor);
        GUILayout.Space(2);
    }

    /// <summary>Draw a colored dot indicator.</summary>
    public static void DrawDot(Rect position, Color color, float size = 8f)
    {
        var dotRect = new Rect(position.x, position.y + (position.height - size) * 0.5f, size, size);
        EditorGUI.DrawRect(dotRect, color);
    }

    /// <summary>Draw a colored tag badge with label text.</summary>
    public static void DrawTagBadge(string label, Color bgColor, Color textColor)
    {
        var style = new GUIStyle(TagBadge);
        style.normal.textColor = textColor;

        var content = new GUIContent(label);
        var size = style.CalcSize(content);
        var rect = GUILayoutUtility.GetRect(size.x, size.y);

        EditorGUI.DrawRect(rect, bgColor);
        GUI.Label(rect, content, style);
    }

    /// <summary>Draw a condition tag badge.</summary>
    public static void DrawConditionTag(string label)
    {
        DrawTagBadge(label, ConditionTagBg, ConditionTagText);
    }

    /// <summary>Draw an action tag badge.</summary>
    public static void DrawActionTag(string label)
    {
        DrawTagBadge(label, ActionTagBg, ActionTagText);
    }

    /// <summary>Get the node color based on its properties.</summary>
    public static Color GetNodeColor(DialogueNode node, bool isFirst)
    {
        if (isFirst) return StartNodeColor;
        if (node.isEnd) return EndNodeColor;
        if (node.HasChoices) return ChoiceNodeColor;
        return LinearNodeColor;
    }

    /// <summary>Get the background color for a choice style.</summary>
    public static Color GetChoiceStyleBg(ChoiceStyle style)
    {
        return style switch
        {
            ChoiceStyle.Empathetic => EmpatheticBg,
            ChoiceStyle.Selfish => SelfishBg,
            _ => NeutralBg
        };
    }

    /// <summary>Get the border color for a choice style.</summary>
    public static Color GetChoiceStyleBorder(ChoiceStyle style)
    {
        return style switch
        {
            ChoiceStyle.Empathetic => EmpatheticBorder,
            ChoiceStyle.Selfish => SelfishBorder,
            _ => NeutralBorder
        };
    }

    /// <summary>Draw a left-colored border on a rect.</summary>
    public static void DrawLeftBorder(Rect rect, Color color, float width = 3f)
    {
        var borderRect = new Rect(rect.x, rect.y, width, rect.height);
        EditorGUI.DrawRect(borderRect, color);
    }
}
