using System;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Nodify.Compatibility
{
    public class WpfControl : TemplatedControl
    {
        /// <summary>
        /// The pointer event that is currently being dispatched to one of the mouse handlers.
        /// Pointer events can be nested (e.g. capturing a pointer synchronously raises PointerExited/PointerEntered),
        /// so the previous value is always restored when a handler returns.
        /// </summary>
        protected PointerEventArgs? currentPointerArgs;

        /// <summary>
        /// The pointer captured through <see cref="CaptureMouseSafe"/>. Allows querying and releasing the capture
        /// outside of a pointer event handler (e.g. from a key handler).
        /// </summary>
        protected IPointer? capturedPointer;

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            var previous = currentPointerArgs;
            currentPointerArgs = e;
            try
            {
                OnMouseEnter(e);
            }
            finally
            {
                currentPointerArgs = previous;
            }

            if (!e.Handled)
                base.OnPointerEntered(e);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            var previous = currentPointerArgs;
            currentPointerArgs = e;
            try
            {
                OnMouseLeave(e);
            }
            finally
            {
                currentPointerArgs = previous;
            }

            if (!e.Handled)
                base.OnPointerExited(e);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            var previous = currentPointerArgs;
            currentPointerArgs = e;
            try
            {
                OnMouseMove(new MouseMoveEventArgs(e));
            }
            finally
            {
                currentPointerArgs = previous;
            }

            if (!e.Handled)
                base.OnPointerMoved(e);
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            var previous = currentPointerArgs;
            currentPointerArgs = e;
            try
            {
                OnMouseDown(new MouseButtonEventArgs(e));
            }
            finally
            {
                currentPointerArgs = previous;
            }

            if (!e.Handled)
                base.OnPointerPressed(e);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            var previous = currentPointerArgs;
            currentPointerArgs = e;
            try
            {
                OnMouseUp(new MouseButtonEventArgs(e));
            }
            finally
            {
                currentPointerArgs = previous;
            }

            if (!e.Handled)
                base.OnPointerReleased(e);
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            if (ReferenceEquals(capturedPointer, e.Pointer))
            {
                // Capture was taken by another element or dropped by the platform.
                capturedPointer = null;
                this.PropagateMouseCapturedWithin(false);
            }

            base.OnPointerCaptureLost(e);
        }

        protected virtual void OnMouseEnter(PointerEventArgs e)
        {
        }

        protected virtual void OnMouseLeave(PointerEventArgs e)
        {
        }

        protected virtual void OnMouseMove(MouseEventArgs e)
        {
        }

        protected virtual void OnMouseDown(MouseButtonEventArgs e)
        {
        }

        protected virtual void OnMouseUp(MouseButtonEventArgs e)
        {
        }

        /// <summary>
        /// Captures the pointer of the event currently being dispatched. Must be called from within a mouse event handler.
        /// </summary>
        protected void CaptureMouseSafe()
        {
            if (currentPointerArgs == null)
                throw new InvalidOperationException($"You may only call {nameof(CaptureMouseSafe)} from within a mouse event handler (e.g. {nameof(OnMouseDown)}).");

            CaptureMouse(currentPointerArgs.Pointer);
        }

        /// <summary>
        /// Captures the specified pointer and remembers it so that <see cref="IsMouseCaptured"/> and <see cref="ReleaseMouseCapture"/>
        /// keep working after the pointer event that provided it has completed.
        /// </summary>
        protected void CaptureMouse(IPointer pointer)
        {
            // A control tracks a single captured pointer at a time; release the previous one instead of forgetting it.
            if (capturedPointer != null && !ReferenceEquals(capturedPointer, pointer))
            {
                ReleasePointer(capturedPointer);
            }

            // Capturing may synchronously raise nested pointer events (PointerExited/PointerEntered/PointerCaptureLost);
            // record the pointer first so that the state is consistent for them.
            capturedPointer = pointer;
            pointer.Capture(this);

            if (ReferenceEquals(pointer.Captured, this))
            {
                this.PropagateMouseCapturedWithin(true);
            }
            else if (ReferenceEquals(capturedPointer, pointer))
            {
                capturedPointer = null;
            }
        }

        /// <summary>
        /// Releases the pointer captured by this control, if any. Can be called outside of a pointer event handler.
        /// </summary>
        protected virtual void ReleaseMouseCapture()
        {
            IPointer? pointer = GetCapturePointer();
            if (pointer != null)
            {
                ReleasePointer(pointer);
            }
        }

        /// <summary>
        /// Releases the specified pointer if it is captured by this control and forgets it if it is the tracked pointer.
        /// Never releases a capture owned by another element and never forgets a different tracked pointer.
        /// </summary>
        private void ReleasePointer(IPointer pointer)
        {
            if (ReferenceEquals(capturedPointer, pointer))
            {
                capturedPointer = null;
            }

            if (ReferenceEquals(pointer.Captured, this))
            {
                pointer.Capture(null);
                this.PropagateMouseCapturedWithin(false);
            }
        }

        /// <summary>
        /// Whether the pointer of the current event (or the pointer previously captured by <see cref="CaptureMouseSafe"/>) is captured by this control.
        /// </summary>
        public bool IsMouseCaptured
        {
            get
            {
                IPointer? pointer = GetCapturePointer();
                return pointer != null && ReferenceEquals(pointer.Captured, this);
            }
        }

        private IPointer? GetCapturePointer()
            => currentPointerArgs?.Pointer ?? capturedPointer;
    }
}
