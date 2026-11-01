using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Nodify.Compatibility;
using Xunit;

namespace Nodify.Tests
{
    public class TestCaptureControl : WpfControl
    {
        public IPointer? Tracked => capturedPointer;
        public void Capture(IPointer pointer) => CaptureMouse(pointer);
        public void Release() => ReleaseMouseCapture();

        /// <summary>Calls <see cref="WpfControl.ReleaseMouseCapture"/> as if from within a pointer event of <paramref name="pointer"/>.</summary>
        public void ReleaseFromEventOf(IPointer pointer, Window root)
        {
            currentPointerArgs = new PointerEventArgs(InputElement.PointerMovedEvent, this, pointer, root, default, 0,
                PointerPointProperties.None, KeyModifiers.None);
            try
            {
                ReleaseMouseCapture();
            }
            finally
            {
                currentPointerArgs = null;
            }
        }
    }

    public class WpfControlCaptureTests
    {
        private static (Window window, NodifyEditor editor, TestCaptureControl control) CreateScene()
        {
            var control = new TestCaptureControl { Width = 10, Height = 10 };
            var editor = new NodifyEditor
            {
                // Minimal template with the parts NodifyEditor requires, plus our control as a descendant.
                Template = new FuncControlTemplate<NodifyEditor>((_, ns) => new Panel
                {
                    Children =
                    {
                        new ItemsPresenter { Name = "PART_ItemsPresenter" }.RegisterInNameScope(ns),
                        new ItemsControl { Name = "PART_ConnectionsHost" }.RegisterInNameScope(ns),
                        control,
                    }
                }),
            };

            var window = new Window { Width = 100, Height = 100, Content = editor };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            editor.ApplyTemplate();
            return (window, editor, control);
        }

        private static Pointer NewPointer(int id) => new(id, PointerType.Touch, isPrimary: id == 1);

        [AvaloniaFact]
        public void Capture_tracks_pointer_and_propagates_capture_within()
        {
            var (_, editor, control) = CreateScene();
            var p1 = NewPointer(1);

            control.Capture(p1);

            Assert.Same(control, p1.Captured);
            Assert.Same(p1, control.Tracked);
            Assert.True(control.IsMouseCaptured);
            Assert.True(editor.IsMouseCaptureWithin);

            control.Release();

            Assert.Null(p1.Captured);
            Assert.Null(control.Tracked);
            Assert.False(control.IsMouseCaptured);
            Assert.False(editor.IsMouseCaptureWithin);
        }

        [AvaloniaFact]
        public void Capturing_a_second_pointer_releases_the_first_instead_of_forgetting_it()
        {
            var (_, _, control) = CreateScene();
            var p1 = NewPointer(1);
            var p2 = NewPointer(2);

            control.Capture(p1);
            control.Capture(p2);

            Assert.Null(p1.Captured);
            Assert.Same(control, p2.Captured);
            Assert.Same(p2, control.Tracked);

            control.Release();
            Assert.Null(p2.Captured);
            Assert.Null(control.Tracked);
        }

        [AvaloniaFact]
        public void Release_from_a_different_pointers_event_keeps_the_tracked_capture()
        {
            var (window, editor, control) = CreateScene();
            var p1 = NewPointer(1);
            var p2 = NewPointer(2);

            control.Capture(p1);
            control.ReleaseFromEventOf(p2, window);

            Assert.Same(control, p1.Captured);
            Assert.Same(p1, control.Tracked);
            Assert.True(editor.IsMouseCaptureWithin);

            control.Release();
            Assert.Null(p1.Captured);
            Assert.Null(control.Tracked);
            Assert.False(editor.IsMouseCaptureWithin);
        }

        [AvaloniaFact]
        public void Release_never_takes_capture_from_another_owner()
        {
            var (window, _, control) = CreateScene();
            var other = new Border();
            var p1 = NewPointer(1);
            p1.Capture(other);

            control.ReleaseFromEventOf(p1, window);
            control.Release();

            Assert.Same(other, p1.Captured);
        }

        [AvaloniaFact]
        public void Capture_lost_to_another_owner_clears_tracking_and_capture_within()
        {
            var (_, editor, control) = CreateScene();
            var other = new Border();
            var p1 = NewPointer(1);

            control.Capture(p1);
            Assert.True(editor.IsMouseCaptureWithin);

            p1.Capture(other);

            Assert.Null(control.Tracked);
            Assert.False(control.IsMouseCaptured);
            Assert.False(editor.IsMouseCaptureWithin);

            control.Release();
            Assert.Same(other, p1.Captured);
        }
    }
}
