using Nodify.UndoRedo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Nodify.Shapes.Canvas.UndoRedo
{
    public class MoveShapesAction : IAction
    {
        private readonly IReadOnlyCollection<ShapeViewModel> _movedShapes;
        private readonly Dictionary<ShapeViewModel, Point> _initialLocations;
        private Dictionary<ShapeViewModel, Point>? _finalLocations;

        public MoveShapesAction(CanvasViewModel canvas)
            : this(SnapshotSelection(canvas))
        {
        }

        internal MoveShapesAction(IReadOnlyCollection<ShapeViewModel> shapes)
        {
            _movedShapes = shapes;
            _initialLocations = _movedShapes.ToDictionary(x => x, x => x.Location);
        }

        /// <summary>
        /// Takes a stable copy of the current selection so that later selection changes don't affect the recorded action.
        /// </summary>
        internal static IReadOnlyCollection<ShapeViewModel> SnapshotSelection(CanvasViewModel canvas)
        {
            var shapes = canvas.SelectedShapes.ToArray();
            if (Array.IndexOf(shapes, null) >= 0)
            {
                throw new InvalidOperationException($"{nameof(CanvasViewModel.SelectedShapes)} contains a null shape.");
            }

            return shapes;
        }

        public string? Label => "Move shapes";

        public void Execute()
        {
            _finalLocations?.ForEach(x => x.Key.Location = x.Value);
        }

        public void Undo()
        {
            _initialLocations.ForEach(x => x.Key.Location = x.Value);
        }

        public void SaveLocations()
        {
            _finalLocations = _movedShapes.ToDictionary(x => x, x => x.Location);
        }
    }
}
