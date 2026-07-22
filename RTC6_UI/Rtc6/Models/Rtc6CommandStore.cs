using System;
using System.Collections.Generic;

namespace RTC6_UI.Rtc6.Models
{
    /// <summary>
    /// RTC6 List에 기록하기 위해 변환된 이동 명령 목록을 저장하고 관리합니다.
    /// 외부에서는 명령 목록을 읽을 수 있지만 직접 수정할 수 없습니다.
    /// </summary>
    public sealed class Rtc6CommandStore
    {
        /// <summary>
        /// 현재 저장된 RTC6 이동 명령 목록입니다.
        /// </summary>
        private readonly List<Rtc6MotionCommand> _commands = new();

        public IReadOnlyList<Rtc6MotionCommand> Commands => _commands;

        /// <summary>
        /// 현재 저장된 명령 개수입니다.
        /// </summary>
        public int Count => _commands.Count;

        /// <summary>
        /// 저장된 명령이 하나 이상 있는지 나타냅니다.
        /// </summary>
        public bool HasCommands => _commands.Count > 0;

        /// <summary>
        /// 기존 명령을 모두 제거하고 새로운 명령 목록으로 교체합니다.
        /// </summary>
        public void Replace(IEnumerable<Rtc6MotionCommand> commands)
        {
            ArgumentNullException.ThrowIfNull(commands);

            _commands.Clear();
            _commands.AddRange(commands);
        }

        /// <summary>
        /// 하나의 RTC6 이동 명령을 목록 끝에 추가합니다.
        /// </summary>
        public void Add(Rtc6MotionCommand command)
        {
            _commands.Add(command);
        }

        /// <summary>
        /// 여러 RTC6 이동 명령을 목록 끝에 추가합니다.
        /// </summary>
        public void AddRange(IEnumerable<Rtc6MotionCommand> commands)
        {
            ArgumentNullException.ThrowIfNull(commands);
            _commands.AddRange(commands);
        }

        /// <summary>
        /// 현재 저장된 모든 RTC6 이동 명령을 삭제합니다.
        /// </summary>
        public void Clear()
        {
            _commands.Clear();
        }
    }
}