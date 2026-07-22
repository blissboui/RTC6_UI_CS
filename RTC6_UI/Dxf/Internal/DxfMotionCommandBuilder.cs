using System.Collections.Generic;
using RTC6_UI.Dxf;
using RTC6_UI.Dxf.Models;

namespace RTC6_UI.Dxf.Internal
{
    /// <summary>
    /// 정리된 Contour를 mm 단위의 Jump 및 Mark 명령 목록으로 변환합니다.
    /// 각 Contour의 첫 점은 Jump로, 이후 점은 Mark로 생성합니다.
    /// </summary>
    internal sealed class DxfMotionCommandBuilder
    {
        /// <summary>
        /// 여러 개의 DxfContour(윤곽선)를 받아서 RTC6에서 사용하기 쉬운 형태의 Jump, Mark 명령 목록으로 바꾸는 함수
        /// </summary>
        /// <param name="contours"></param>
        /// <returns></returns>
        public List<DxfMotionCommand> BuildMotionCommands(
            IReadOnlyList<DxfContour> contours)
        {
            // 
            List<DxfMotionCommand> commands = new();

            foreach (DxfContour contour in contours)
            {
                if (contour.Points.Count < 2)
                {
                    continue;
                }

                DxfPathPoint first = contour.Points[0];

                commands.Add(   // 시작 좌표 Jump
                    new DxfMotionCommand(
                        DxfMotionType.Jump,
                        first.X,
                        first.Y,
                        contour.LayerName
                    )
                );

                for (int index = 1; index < contour.Points.Count; index++)  // mm단위 좌표 -> rtc명령 데이터로 변환 (DxfMotionCommand)
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

                if (contour.IsClosed)   // 닫힌도형
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
