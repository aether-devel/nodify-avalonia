using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Nodify;
using Xunit;

namespace Nodify.Tests
{
    public class TestConnector : Connector
    {
        public bool IsCapturedPublic => IsMouseCaptured;
        public void ReleasePublic() => ReleaseMouseCapture();
        public IPointer? CapturedPointerField => capturedPointer;
    }

    public class ConnectorCaptureTests : IDisposable
    {
        private static readonly Point Center = new(50, 50);

        private readonly bool originalStickyConnections = Connector.EnableStickyConnections;

        public void Dispose() => Connector.EnableStickyConnections = originalStickyConnections;

        private static (Window window, TestConnector connector, Func<IPointer?> pointer) CreateScene(bool sticky = false)
        {
            Connector.EnableStickyConnections = sticky;
            var connector = new TestConnector
            {
                Width = 100,
                Height = 100,
                Template = new FuncControlTemplate<Connector>((c, ns) =>
                    new Border { Name = "PART_Connector", Background = Brushes.Red }.RegisterInNameScope(ns)),
            };

            IPointer? pointer = null;
            connector.AddHandler(InputElement.PointerPressedEvent, (s, e) => pointer = e.Pointer, RoutingStrategies.Tunnel);

            var window = new Window { Width = 100, Height = 100, Content = connector };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            connector.ApplyTemplate();
            return (window, connector, () => pointer);
        }

        [AvaloniaFact]
        public void Press_starts_pending_connection_and_captures_pointer_without_throwing()
        {
            var (window, connector, pointer) = CreateScene();

            window.MouseDown(Center, MouseButton.Left);

            Assert.True(connector.IsPendingConnection);
            Assert.NotNull(pointer());
            Assert.Same(connector, pointer()!.Captured);
            Assert.True(connector.IsCapturedPublic);
        }

        [AvaloniaFact]
        public void Nested_pointer_event_raised_from_started_handler_does_not_break_outer_capture_context()
        {
            var (window, connector, pointer) = CreateScene();
            bool nestedRan = false;

            connector.AddHandler(Connector.PendingConnectionStartedEvent, (s, e) =>
            {
                var p = pointer()!;
                // Simulate Avalonia re-entering the pointer pipeline (e.g. PointerExited/Entered raised by Pointer.Capture or app code).
                connector.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, connector, p, window, Center, 0,
                    new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other), KeyModifiers.None));
                connector.RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, connector, p, window, Center, 0,
                    PointerPointProperties.None, KeyModifiers.None));
                nestedRan = true;
            });

            var ex = Record.Exception(() => window.MouseDown(Center, MouseButton.Left));

            Assert.Null(ex);
            Assert.True(nestedRan);
            Assert.True(connector.IsPendingConnection);
            Assert.Same(connector, pointer()!.Captured);
        }

        [AvaloniaFact]
        public void Canceled_started_event_releases_capture()
        {
            var (window, connector, pointer) = CreateScene();
            connector.AddHandler(Connector.PendingConnectionStartedEvent, (s, e) => e.Canceled = true);

            window.MouseDown(Center, MouseButton.Left);

            Assert.False(connector.IsPendingConnection);
            Assert.Null(pointer()!.Captured);
            Assert.False(connector.IsCapturedPublic);
            Assert.Null(connector.CapturedPointerField);
        }

        [AvaloniaFact]
        public void Release_completes_connection_and_releases_capture()
        {
            var (window, connector, pointer) = CreateScene();
            var completed = new List<bool>();
            connector.AddHandler(Connector.PendingConnectionCompletedEvent, (s, e) => completed.Add(e.Canceled));

            window.MouseDown(Center, MouseButton.Left);
            window.MouseUp(Center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { false }, completed);
            Assert.False(connector.IsPendingConnection);
            Assert.Null(pointer()!.Captured);
            Assert.Null(connector.CapturedPointerField);
        }

        [AvaloniaFact]
        public void Escape_after_press_cancels_and_releases_capture_outside_pointer_event()
        {
            var (window, connector, pointer) = CreateScene();
            var completed = new List<bool>();
            connector.AddHandler(Connector.PendingConnectionCompletedEvent, (s, e) => completed.Add(e.Canceled));

            window.MouseDown(Center, MouseButton.Left);
            Assert.Same(connector, pointer()!.Captured);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);

            Assert.Equal(new[] { true }, completed);
            Assert.False(connector.IsPendingConnection);
            Assert.Null(pointer()!.Captured);
            Assert.False(connector.IsCapturedPublic);
        }

        [AvaloniaFact]
        public void Sticky_connection_recaptures_after_release_and_escape_releases_it()
        {
            var (window, connector, pointer) = CreateScene(sticky: true);
            var completed = new List<bool>();
            connector.AddHandler(Connector.PendingConnectionCompletedEvent, (s, e) => completed.Add(e.Canceled));

            window.MouseDown(Center, MouseButton.Left);
            window.MouseUp(Center, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(completed);
            Assert.True(connector.IsPendingConnection);
            Assert.Same(connector, pointer()!.Captured);
            Assert.True(connector.IsCapturedPublic);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(new[] { true }, completed);
            Assert.False(connector.IsPendingConnection);
            Assert.Null(pointer()!.Captured);
            Assert.Null(connector.CapturedPointerField);
        }

        [AvaloniaFact]
        public void Release_does_not_steal_capture_owned_by_another_element()
        {
            var (window, connector, pointer) = CreateScene();
            window.MouseDown(Center, MouseButton.Left);
            var p = pointer()!;
            Assert.Same(connector, p.Captured);

            var other = new Border();
            p.Capture(other);
            Dispatcher.UIThread.RunJobs();

            Assert.False(connector.IsPendingConnection); // capture lost cancels
            Assert.Null(connector.CapturedPointerField);
            Assert.False(connector.IsCapturedPublic);

            connector.ReleasePublic();
            Assert.Same(other, p.Captured);
        }
    }
}
