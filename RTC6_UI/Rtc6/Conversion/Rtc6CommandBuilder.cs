using RTC6_UI.Dxf;
using RTC6_UI.Rtc6.Models;
using RTC6_UI.Settings;
using System;
using System.Collections.Generic;


namespace RTC6_UI.Rtc6.Conversion
{
    /// <summary>
    /// mm 단위 DXF 이동 명령 목록을 RTC6 List 작성에 사용할 정수 좌표 명령 목록으로 생성합니다.
    /// DXF 도면 중심 이동과 RTC6 정수 좌표 변환 과정을 순서대로 수행합니다.
    /// </summary>
    public sealed class Rtc6CommandBuilder
    {
        /// <summary>
        /// DXF 명령 목록의 도면 중심을 (0,0)으로 이동한 후 현재 시스템 설정을 적용하여 RTC6 정수 좌표 명령 목록을 생성합니다.
        /// 원본 DXF 명령 목록은 변경하지 않습니다.
        /// </summary>
        /// <param name="sourceCommands">mm 단위 좌표를 가진 원본 DXF Jump/Mark 명령 목록입니다.</param>
        /// <param name="settings">필드 크기, Bits/mm 및 축 반전 설정이 들어 있는 현재 시스템 설정입니다.</param>
        /// <returns>RTC6 List 작성에 사용할 정수 좌표 Jump/Mark 명령 목록입니다.</returns>
        public List<Rtc6MotionCommand> BuildRtc6Commands(IReadOnlyList<DxfMotionCommand> sourceCommands, SystemSettings settings, ModelSettings modelSettings)
        {
            ArgumentNullException.ThrowIfNull(sourceCommands);
            ArgumentNullException.ThrowIfNull(settings);

            if (sourceCommands.Count == 0) 
                throw new ArgumentException("변환할 DXF 이동 명령이 없습니다.", nameof(sourceCommands));

            // DXF 도면의 중심을 계산하고 모든 mm 좌표를 중심 기준 좌표로 이동합니다.
            DxfCommandCenteringConverter centeringConverter = new();
            List<DxfMotionCommand> centeredCommands = centeringConverter.Convert(sourceCommands);

            // 배율, 패턴 오프셋 적용
            Rtc6PatternTransformer patternTransformer = new Rtc6PatternTransformer();
            List<DxfMotionCommand> patternCommands = patternTransformer.Transform(centeredCommands, modelSettings);

            // 현재 시스템 설정을 사용하여 중심 이동된 mm 좌표를 RTC6 정수 좌표로 변환합니다.
            Rtc6CommandConverter commandConverter = new(settings);
            List<Rtc6MotionCommand> convertedCommands = commandConverter.Convert(centeredCommands);

            return convertedCommands;
        }
    }
}