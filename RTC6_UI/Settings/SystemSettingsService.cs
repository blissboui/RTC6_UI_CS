using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RTC6_UI.Services;

namespace RTC6_UI.Settings
{
    /// <summary>
    /// 시스템 설정을 system.json 파일에 저장하고 불러옵니다.
    /// RTC6 파일 검사는 Rtc6FileValidator를 통해 수행합니다.
    /// </summary>
    public sealed class SystemSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// 마지막으로 발생한 오류 내용입니다.
        /// </summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// system.json이 저장되는 전체 경로입니다.
        /// </summary>
        public string SettingsFilePath { get; }

        /// <summary>
        /// 실행 파일 아래의 Settings\system.json을 사용합니다.
        /// </summary>
        public SystemSettingsService()
        {
            SettingsFilePath = Path.Combine(
                AppContext.BaseDirectory,
                "Settings",
                "system.json");
        }

        /// <summary>
        /// system.json을 읽어 시스템 설정을 반환합니다.
        /// 파일이 없으면 기본 설정을 생성하여 저장합니다.
        /// </summary>
        public bool Load(out SystemSettings settings)
        {
            LastError = string.Empty;
            settings = new SystemSettings();

            try
            {
                if (!File.Exists(SettingsFilePath))
                    return Save(settings);

                string json = File.ReadAllText(
                    SettingsFilePath,
                    Encoding.UTF8);

                settings =
                    JsonSerializer.Deserialize<SystemSettings>(
                        json,
                        JsonOptions)
                    ?? new SystemSettings();

                if (!SystemSettingsValidator.Validate(
                        settings,
                        out string validationError))
                {
                    LastError =
                        "시스템 설정값이 올바르지 않습니다.\n"
                        + validationError;

                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                LastError = BuildExceptionMessage(
                    "시스템 설정을 불러오지 못했습니다.",
                    exception);

                return false;
            }
        }

        /// <summary>
        /// 시스템 설정값을 검사한 후 system.json에 저장합니다.
        /// </summary>
        public bool Save(SystemSettings settings)
        {
            LastError = string.Empty;

            if (settings is null)
            {
                LastError = "저장할 시스템 설정값이 없습니다.";
                return false;
            }

            if (!SystemSettingsValidator.Validate(
                    settings,
                    out string validationError))
            {
                LastError =
                    "시스템 설정값이 올바르지 않습니다.\n"
                    + validationError;

                return false;
            }

            try
            {
                string? directory = Path.GetDirectoryName(SettingsFilePath);

                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                string json = JsonSerializer.Serialize(settings, JsonOptions);

                string temporaryPath = SettingsFilePath + ".tmp";

                File.WriteAllText(
                    temporaryPath,
                    json,
                    new UTF8Encoding(false));

                File.Move(
                    temporaryPath,
                    SettingsFilePath,
                    true);

                return true;
            }
            catch (Exception exception)
            {
                LastError = BuildExceptionMessage(
                    "시스템 설정을 저장하지 못했습니다.",
                    exception);

                return false;
            }
        }

        /// <summary>
        /// 설정에 저장된 RTC6 폴더 경로를 확인하고,
        /// 폴더 내부의 필수 파일이 존재하는지 검사합니다.
        /// </summary>
        public bool TryResolveRtc6Paths(
            SystemSettings settings,
            out string folderPath,
            out string correctionFilePath)
        {
            LastError = string.Empty;
            folderPath = string.Empty;
            correctionFilePath = string.Empty;

            if (settings is null)
            {
                LastError = "시스템 설정값이 없습니다.";
                return false;
            }

            bool result = Rtc6FileValidator.TryValidate(
                settings.Rtc6FilesFolder,
                settings.CorrectionFileName,
                out folderPath,
                out correctionFilePath,
                out string error);

            if (!result)
            {
                LastError = error;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 가장 안쪽에서 발생한 실제 예외 내용을 사용자 메시지로 만듭니다.
        /// </summary>
        private static string BuildExceptionMessage(
            string title,
            Exception exception)
        {
            Exception root = exception;

            while (root.InnerException is not null)
                root = root.InnerException;

            return
                $"{title}\n"
                + $"종류: {root.GetType().Name}\n"
                + $"내용: {root.Message}";
        }
    }
}