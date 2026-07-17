using System.Collections.Generic;
using RTC6_UI.Dxf.Models;

namespace RTC6_UI.Dxf.Internal
{
    internal sealed class DxfMotionCommandBuilder
    {
        public List<DxfMotionCommand> BuildMotionCommands(
            IReadOnlyList<DxfContour> contours)
        {
            List<DxfMotionCommand> commands = new();

            foreach (DxfContour contour in contours)
            {
                if (contour.Points.Count < 2)
                {
                    continue;
                }

                DxfPathPoint first = contour.Points[0];

                commands.Add(
                    new DxfMotionCommand(
                        DxfMotionType.Jump,
                        first.X,
                        first.Y,
                        contour.LayerName
                    )
                );

                for (int index = 1; index < contour.Points.Count; index++)
                {
                    DxfPathPoint point = contour.Points[index];

                    commands.Add(
                        new DxfMotionCommand(
                            DxfMotionType.Mark,
                            point.X,
                            point.Y,
                            contour.LayerName
                        )
                    );
                }

                if (contour.IsClosed)
                {
                    commands.Add(
                        new DxfMotionCommand(
                            DxfMotionType.Mark,
                            first.X,
                            first.Y,
                            contour.LayerName
                        )
                    );
                }
            }

            return commands;
        }
    }
}
