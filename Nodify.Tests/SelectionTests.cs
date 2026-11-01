using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls.Selection;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Nodify;
using Nodify.Shapes.Canvas;
using Nodify.Shapes.Canvas.UndoRedo;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Nodify.Tests.TestApp))]

namespace Nodify.Tests
{
    public class TestApp : Application
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    public class TestEditor : NodifyEditor
    {
        public ISelectionModel Model => Selection;
    }

    public class EditorSelectionBridgeTests
    {
        [AvaloniaFact]
        public void Selecting_index_before_items_source_does_not_add_null_to_bound_selection()
        {
            var bound = new ObservableCollection<object>();
            var editor = new TestEditor { SelectedItems = bound };

            editor.Model.Select(0);

            Assert.DoesNotContain(null, bound);
            Assert.Empty(bound);
        }

        [AvaloniaFact]
        public void Replacing_items_source_does_not_add_null_to_bound_selection()
        {
            var bound = new ObservableCollection<object>();
            var items = new ObservableCollection<string> { "a", "b", "c" };
            var editor = new TestEditor { ItemsSource = items, SelectedItems = bound };

            editor.Model.Select(1);
            Assert.Equal(new object[] { "b" }, bound.ToArray());

            editor.ItemsSource = null;
            Assert.DoesNotContain(null, bound);

            editor.ItemsSource = new ObservableCollection<string> { "x", "y" };
            editor.Model.Select(0);
            Assert.DoesNotContain(null, bound);
            Assert.All(bound, x => Assert.IsType<string>(x));
        }

        [AvaloniaFact]
        public void Normal_selection_round_trips_into_bound_collection()
        {
            var bound = new ObservableCollection<object>();
            var items = new ObservableCollection<string> { "a", "b", "c" };
            var editor = new TestEditor { ItemsSource = items, SelectedItems = bound };

            editor.Model.Select(0);
            editor.Model.Select(2);
            Assert.Equal(new object[] { "a", "c" }, bound.ToArray());

            editor.Model.Deselect(0);
            Assert.Equal(new object[] { "c" }, bound.ToArray());

            bound.Add("b");
            Assert.True(editor.Model.IsSelected(1));
            bound.Remove("c");
            Assert.False(editor.Model.IsSelected(2));
        }
    }

    public class MoveShapesActionTests
    {
        [AvaloniaFact]
        public void Move_action_snapshots_selection_and_undo_redo_are_sound()
        {
            var canvas = new CanvasViewModel();
            canvas.UndoRedo.IsEnabled = true;
            var shape = canvas.Shapes.First();
            var other = canvas.Shapes.Skip(1).First();
            canvas.SelectedShapes.Clear();
            canvas.SelectedShapes.Add(shape);
            var start = shape.Location;
            var otherStart = other.Location;

            var action = new MoveShapesAction(canvas);
            // selection changes mid-drag must not leak into the recorded action
            canvas.SelectedShapes.Add(other);
            shape.Location = new Avalonia.Point(999, 999);
            other.Location = new Avalonia.Point(-5, -5);
            action.SaveLocations();

            action.Undo();
            Assert.Equal(start, shape.Location);
            Assert.Equal(new Avalonia.Point(-5, -5), other.Location); // untouched by the action

            action.Execute();
            Assert.Equal(new Avalonia.Point(999, 999), shape.Location);
        }

        [AvaloniaFact]
        public void Resize_action_snapshots_selection_and_undo_is_sound()
        {
            var canvas = new CanvasViewModel();
            var shape = canvas.Shapes.First();
            canvas.SelectedShapes.Clear();
            canvas.SelectedShapes.Add(shape);
            var start = shape.Location;
            var w = shape.Width;

            var action = new ResizeShapesAction(canvas);
            shape.Width = w + 50;
            shape.Location = new Avalonia.Point(1, 2);
            action.SaveSizes();

            action.Undo();
            Assert.Equal(w, shape.Width);
            Assert.Equal(start, shape.Location);
            action.Execute();
            Assert.Equal(w + 50, shape.Width);
            Assert.Equal(new Avalonia.Point(1, 2), shape.Location);
        }

        [AvaloniaFact]
        public void Null_in_selection_fails_fast_with_descriptive_error()
        {
            var canvas = new CanvasViewModel();
            canvas.SelectedShapes.Add(null!);

            var ex = Assert.Throws<InvalidOperationException>(() => new MoveShapesAction(canvas));
            Assert.Contains("SelectedShapes", ex.Message);
            Assert.Throws<InvalidOperationException>(() => new ResizeShapesAction(canvas));
        }
    }
}
