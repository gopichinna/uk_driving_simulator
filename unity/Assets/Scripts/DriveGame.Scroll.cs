using UnityEngine;

namespace LToLocal.Game
{
    /// <summary>
    /// Drag-to-scroll for the menus. Unity's immediate-mode scroll view only scrolls by its thin scrollbar, which is hard to
    /// hit on a phone, and a finger that starts on a button never scrolls. Here a finger that moves more than a few pixels
    /// inside the menu's list scrolls it (with a little momentum), and the buttons are switched off while it moves so that
    /// letting go does not press one.
    /// </summary>
    public sealed partial class DriveGame
    {
        private Rect _touchScrollRect;               // the list's rectangle in GUI space, set by the menu that is showing
        private Vector2 _noScroll;
        private int _scrollFinger = -1;
        private float _scrollVelocity;
        private bool _scrollDragging, _scrollBlock;
        private int _scrollBlockFrames;
        private Vector2 _scrollStart;

        private ref Vector2 ScrollFor(Mode m)
        {
            if (m == Mode.LessonMenu) return ref _menuScroll;
            if (m == Mode.TheoryMenu) return ref _theoryScroll;
            return ref _noScroll;
        }

        private void UpdateTouchScroll()
        {
            if (_mode != Mode.LessonMenu && _mode != Mode.TheoryMenu)
            {
                _scrollFinger = -1; _scrollDragging = _scrollBlock = false; _scrollVelocity = 0;
                _touchScrollRect = Rect.zero;
                return;
            }
            ref Vector2 scroll = ref ScrollFor(_mode);

            // Keep the buttons off for a couple of frames after a drag ends, so the release is not a tap.
            if (_scrollBlock && !_scrollDragging && --_scrollBlockFrames <= 0) _scrollBlock = false;

            bool any = false;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                Vector2 gui = new Vector2(t.position.x, Screen.height - t.position.y);
                if (t.phase == TouchPhase.Began && _scrollFinger < 0 && _touchScrollRect.Contains(gui))
                {
                    _scrollFinger = t.fingerId;
                    _scrollStart = gui;
                    _scrollDragging = false;
                    _scrollVelocity = 0;
                }
                if (t.fingerId != _scrollFinger) continue;
                any = true;
                if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary)
                {
                    if (!_scrollDragging && Mathf.Abs(gui.y - _scrollStart.y) > Screen.height * 0.012f)
                    {
                        _scrollDragging = true;
                        _scrollBlock = true;
                    }
                    if (_scrollDragging)
                    {
                        float dy = t.deltaPosition.y; // finger up = list moves up
                        scroll.y += dy;
                        _scrollVelocity = dy / Mathf.Max(Time.unscaledDeltaTime, 0.001f);
                    }
                }
                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    _scrollFinger = -1;
                    if (_scrollDragging) { _scrollDragging = false; _scrollBlockFrames = 3; }
                }
            }

            if (!any && _scrollFinger < 0 && Mathf.Abs(_scrollVelocity) > 30f)
            {
                scroll.y += _scrollVelocity * Time.unscaledDeltaTime;
                _scrollVelocity *= Mathf.Pow(0.08f, Time.unscaledDeltaTime); // glide to a stop in about a second
                _scrollBlock = true;
                _scrollBlockFrames = 3;
            }
            else if (!any) _scrollVelocity = 0;

            if (scroll.y < 0) { scroll.y = 0; _scrollVelocity = 0; }
        }
    }
}
