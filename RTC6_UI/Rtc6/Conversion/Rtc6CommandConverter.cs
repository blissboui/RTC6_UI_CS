using RTC6_UI.Dxf;
using RTC6_UI.Rtc6.Models;
using RTC6_UI.Settings;


namespace RTC6_UI.Rtc6.Conversion
{
    /// <summary>
    /// DXF에서 생성된 mm 단위 Jump/Mark 명령 목록을
    /// RTC6 List 작성에 사용할 정수 좌표 명령 목록으로 변환합니다.
    /// 좌표 변환은 Rtc6CoordinateConverter에 위임합니다.
    /// </summary>
    public sealed class Rtc6CommandConverter
    {
        private readonly Rtc6CoordinateConverter _coordinateConverter;

        /// <summary>
        /// 좌표 변환에 사용할 현재 시스템 설정을 전달받습니다.
        /// </summary>
        public Rtc6CommandConverter(SystemSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            _coordinateConverter = new Rtc6CoordinateConverter(settings);
        }

        /// <summary>
        /// mm 단위 DXF 이동 명령 목록 전체를 RTC6 정수 좌표 명령 목록으로 변환합니다.
        /// 원본 목록은 변경하지 않고 새로운 목록을 생성하여 반환합니다.
        /// </summary>
        /// <param name="sourceCommands">
        /// DXF에서 생성된 mm 단위 Jump/Mark 명령 목록입니다.
        /// </param>
        /// <returns>
        /// RTC6 List 작성에 사용할 정수 좌표 명령 목록입니다.
        /// </returns>
        public List<Rtc6MotionCommand> Convert(IReadOnlyList<DxfMotionCommand> sourceCommands)
        {
            ArgumentNullException.ThrowIfNull(sourceCommands);


            List<Rtc6MotionCommand> convertedCommands = new(sourceCommands.Count);

            for (int index = 0; index < sourceCommands.Count; index++)
            {
                DxfMotionCommand sourceCommand = sourceCommands[index];

                try
                {
                    Rtc6Point point = _coordinateConverter.Convert(sourceCommand.X, sourceCommand.Y);

                    Rtc6MotionType motionType = ConvertMotionType(sourceCommand.Type);

                    convertedCommands.Add(new Rtc6MotionCommand(motionType, point.X, point.Y));
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"DXF 명령 {index + 1}번의 RTC6 좌표 변환에 실패했습니다. " +
                        $"Type={sourceCommand.Type}, " +
                        $"X={sourceCommand.X:F4}, " +
                        $"Y={sourceCommand.Y:F4}, " +
                        $"Layer={sourceCommand.LayerName}",
                        exception);
                }
            }

            return convertedCommands;
        }

        /// <summary>
        /// DXF 이동 명령 종류를 RTC6 이동 명령 종류로 변환합니다.
        /// </summary>
        private static Rtc6MotionType ConvertMotionType(DxfMotionType motionType)
        {
            return motionType switch
            {
                DxfMotionType.Jump => Rtc6MotionType.Jump,
                DxfMotionType.Mark => Rtc6MotionType.Mark,

                _ => throw new ArgumentOutOfRangeException(nameof(motionType), motionType, "지원하지 않는 DXF 이동 명령 종류입니다.")
            };
        }
    }
}