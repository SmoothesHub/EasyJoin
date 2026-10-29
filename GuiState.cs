using System;
using UnityEngine;

namespace EasyJoin
{
    /// <summary>No Unity callbacks, disk polling, timers or live window movement.</summary>
    internal sealed class GuiState
    {
        internal const float Width = 300, Height = 122, TitleHeight = 26;
        private readonly RoomConfiguration configuration;
        private readonly Func<double> now;
        private bool toggleHeld, saveScheduled;
        private double saveAt;
        private float offsetX, offsetY;
        private int screenWidth, screenHeight;
        internal bool Visible { get; private set; }
        internal bool Dragging { get; private set; }
        internal Rect Window { get; private set; }
        internal Rect Preview { get; private set; }
        internal string Text { get; private set; } = "";

        internal GuiState(RoomConfiguration configuration, Func<double> now)
        {
            this.configuration = configuration;
            this.now = now;
            Window = new Rect(configuration.WindowX, configuration.WindowY, Width, Height);
        }

        internal void ToggleDown()
        {
            if (toggleHeld) return;
            toggleHeld = true;
            if (Visible) Close();
            else
            {
                Flush();
                configuration.Reload();
                Text = configuration.RawCode;
                // Do not let TextField's length limit silently rewrite an oversized disk value.
                if (Text.Length > 64) Text = Text.Substring(0, 64);
                Window = new Rect(configuration.WindowX, configuration.WindowY, Width, Height);
                screenWidth = screenHeight = 0;
                Visible = true;
            }
        }
        internal void ToggleUp() { toggleHeld = false; }
        internal void Close() { Visible = false; CancelDrag(); Flush(); }
        internal void LoseFocus() { toggleHeld = false; CancelDrag(); Flush(); }
        internal void CancelDrag() { Dragging = false; }

        internal bool FitScreen(int width, int height)
        {
            if (width <= 0 || height <= 0) { CancelDrag(); return false; }
            if (width == screenWidth && height == screenHeight) return true;
            screenWidth = width; screenHeight = height;
            // Resolution changes cancel a gesture before repairing its now-invalid bounds.
            CancelDrag();
            Rect old = Window;
            Window = Clamp(new Rect(old.x, old.y, Math.Min(Width, width), Math.Min(Height, height)));
            if (Window.x != old.x || Window.y != old.y)
            {
                configuration.SetPosition(Window.x, Window.y);
                Flush(); // One boundary repair; never a write per repaint or per drag step.
            }
            return true;
        }

        internal bool BeginDrag(float x, float y)
        {
            if (!Visible || Dragging || screenWidth <= 0 || screenHeight <= 0 ||
                !new Rect(Window.x, Window.y, Window.width, Math.Min(TitleHeight, Window.height)).Contains(new Vector2(x, y)))
                return false;
            offsetX = x - Window.x; offsetY = y - Window.y;
            Preview = Window;
            Dragging = true;
            return true;
        }

        internal void MoveDrag(float x, float y)
        {
            if (!Dragging || Single.IsNaN(x) || Single.IsInfinity(x) || Single.IsNaN(y) || Single.IsInfinity(y)) return;
            Preview = Clamp(new Rect(x - offsetX, y - offsetY, Window.width, Window.height));
        }

        internal void EndDrag(float x, float y)
        {
            if (!Dragging) return;
            MoveDrag(x, y); // Also handles a fast release without an intervening MouseDrag.
            Dragging = false;
            if (Window.x == Preview.x && Window.y == Preview.y) return;
            Window = Preview; // The only gesture-driven assignment to the real rectangle.
            configuration.SetPosition(Window.x, Window.y);
            Flush();
        }

        internal void Edit(string text)
        {
            if (text == Text) return;
            Text = text.Replace("\r", "").Replace("\n", "").Replace("\0", "");
            configuration.EditCode(Text);
            saveScheduled = configuration.HasPendingChanges;
            saveAt = now() + 0.35;
        }

        internal void Repaint()
        {
            if (saveScheduled && now() >= saveAt) Flush();
        }

        private void Flush()
        {
            saveScheduled = false;
            configuration.SavePending(); // A failed write is retried on a user action, not every frame.
        }

        internal string Status
        {
            get
            {
                if (configuration.SaveFailed) return "Save failed - close/reopen to retry";
                if (configuration.ReadFailed) return "Can't read CFG - close/reopen to retry";
                if (configuration.HasPendingChanges) return "Saving...";
                if (configuration.CachedCode != null) return "Saved";
                return String.IsNullOrWhiteSpace(Text) ? "Blank - quick join disabled" : "Invalid code - use 1-10 letters/digits";
            }
        }

        private Rect Clamp(Rect rectangle)
        {
            rectangle.x = Math.Max(0, Math.Min(rectangle.x, screenWidth - rectangle.width));
            rectangle.y = Math.Max(0, Math.Min(rectangle.y, screenHeight - rectangle.height));
            return rectangle;
        }
    }
}
