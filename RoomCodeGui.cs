using System;
using UnityEngine;

namespace EasyJoin
{
    /// <summary>One fixed IMGUI panel and four repaint-only lines, all in screen GUI coordinates.</summary>
    internal sealed class RoomCodeGui
    {
        private const string FieldName = "EasyJoin.RoomCode";
        private readonly GuiState state;
        private GUIStyle windowStyle, smallStyle;
        private Texture2D white;
        private bool clearFocus, focused = true;

        internal RoomCodeGui(GuiState state) { this.state = state; }

        internal void Suspend(bool close)
        {
            state.LoseFocus();
            if (close) state.Close();
            clearFocus = true;
        }

        internal void Focus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus) Suspend(false);
        }

        internal void Draw()
        {
            Event current = Event.current;
            if (current == null) return;
            if (clearFocus)
            {
                ClearTextFocus();
                clearFocus = false;
            }
            // rawType allows a release to clear our latch even if another control used the event.
            if (current.rawType == EventType.KeyUp && current.keyCode == KeyCode.LeftAlt) state.ToggleUp();
            if (!focused) return;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.LeftAlt)
            {
                state.ToggleDown();
                ClearTextFocus();
                current.Use();
            }
            else if (state.Visible && current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape)
            {
                state.Close(); ClearTextFocus(); current.Use();
            }
            if (!state.Visible) return; // Closed: no styles, controls, screen queries, or disk access.
            if (!state.FitScreen(Screen.width, Screen.height)) return;

            if (windowStyle == null)
            {
                windowStyle = new GUIStyle(GUI.skin.window)
                {
                    alignment = TextAnchor.UpperCenter, fontSize = 14,
                    padding = new RectOffset(8, 8, 4, 4)
                };
                smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 11 };
                white = Texture2D.whiteTexture; // Unity-owned singleton; never created/destroyed by EasyJoin.
            }

            // A top-level panel avoids Window's local mouse coordinates and implicit window state.
            // Consume the initiating mouse-down before any other controls can capture it. Keeping
            // subsequent events at this root means dragging outside the panel needs no hot-control
            // assignment that could survive a disabled/destroyed component.
            if (state.Dragging && GUIUtility.hotControl != 0) state.CancelDrag();
            if (state.Dragging)
            {
                if (current.rawType == EventType.MouseUp && current.button == 0)
                {
                    state.EndDrag(current.mousePosition.x, current.mousePosition.y);
                    current.Use();
                }
                else if (current.type == EventType.MouseDrag || current.type == EventType.MouseMove)
                {
                    state.MoveDrag(current.mousePosition.x, current.mousePosition.y);
                    current.Use();
                }
                else if (current.type == EventType.Ignore) state.CancelDrag();
            }
            else if (current.type == EventType.MouseDown && current.button == 0 && GUIUtility.hotControl == 0 &&
                state.BeginDrag(current.mousePosition.x, current.mousePosition.y))
            {
                ClearTextFocus(); current.Use();
            }

            if (current.type == EventType.Repaint) state.Repaint();
            Color oldColor = GUI.color;
            int oldDepth = GUI.depth;
            try
            {
                GUI.color = Color.white;
                GUI.depth = -100;
                Rect box = state.Window;
                GUI.Box(box, "Code for E.J.", windowStyle);
                float width = Math.Max(1, box.width - 24);
                GUI.BeginGroup(box); // Clip contents on unusually small/minimized displays.
                try
                {
                    GUI.Label(new Rect(12, 29, width, 20), "Room code:");
                    GUI.SetNextControlName(FieldName);
                    state.Edit(GUI.TextField(new Rect(12, 51, width, 24), state.Text, 64));
                    GUI.Label(new Rect(12, 79, width, 20), state.Status, smallStyle);
                    GUI.Label(new Rect(12, 99, width, 18), "Left Alt: close", smallStyle);
                }
                finally { GUI.EndGroup(); }
                if (state.Dragging && current.type == EventType.Repaint) DrawOutline(state.Preview);
            }
            finally { GUI.color = oldColor; GUI.depth = oldDepth; }
            // Blank areas of our panel should not activate controls behind it.
            if ((current.type == EventType.MouseDown || current.type == EventType.MouseUp) && state.Window.Contains(current.mousePosition))
                current.Use();
        }

        private static void ClearTextFocus()
        {
            if (GUI.GetNameOfFocusedControl() == FieldName) GUI.FocusControl(null);
        }

        private void DrawOutline(Rect rectangle)
        {
            // Exactly four one-pixel white strips. No center, duplicate panel or temporary texture.
            GUI.DrawTexture(new Rect(rectangle.x, rectangle.y, rectangle.width, 1), white);
            GUI.DrawTexture(new Rect(rectangle.x, rectangle.y + rectangle.height - 1, rectangle.width, 1), white);
            GUI.DrawTexture(new Rect(rectangle.x, rectangle.y + 1, 1, Math.Max(0, rectangle.height - 2)), white);
            GUI.DrawTexture(new Rect(rectangle.x + rectangle.width - 1, rectangle.y + 1, 1, Math.Max(0, rectangle.height - 2)), white);
        }
    }
}
