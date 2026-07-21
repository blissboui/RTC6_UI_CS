using System;
using System.IO;

namespace RTC6_UI.Services
{
    /// <summary>
    /// RTC6 초기화에 필요한 폴더와 필수 파일이 실제로 존재하는지 검사합니다.
    /// 설정값 저장이나 RTC6 제어는 수행하지 않고, 파일 경로 검증만 담당합니다.
    /// </summary>
    public static class Rtc6FileValidator
    {
        /// <summary>
        /// 입력한 RTC6 폴더 경로를 절대경로로 변환하고,
        /// 폴더 내부에 RTC6DAT.dat, RTC6OUT.out,
        /// Cor_1to1.ct5 파일이 모두 존재하는지 검사합니다.
        /// </summary>
        /// <param name="folder">
        /// 검사할 RTC6 파일 폴더 경로입니다.
        /// 절대경로와 실행 파일 기준 상대경로를 모두 사용할 수 있습니다.
        /// </param>
        /// <param name="folderPath">
        /// 검사에 성공하면 변환된 RTC6 폴더의 절대경로가 반환됩니다.
        /// </param>
        /// <param name="correctionFilePath">
        /// 검사에 성공하면 Cor_1to1.ct5 파일의 전체 경로가 반환됩니다.
        /// </param>
        /// <param name="error">
        /// 검사에 실패하면 실패 원인이 반환됩니다.
        /// 검사에 성공하면 빈 문자열이 반환됩니다.
        /// </param>
        /// <returns>
        /// 폴더와 필수 파일이 모두 정상적으로 존재하면 true,
        /// 경로가 잘못되었거나 필수 파일이 없으면 false를 반환합니다.
        /// </returns>
        public static bool TryValidate(
            string folder,
            string correctionFileName,
            out string folderPath,
            out string correctionFilePath,
            out string error)
        {
            folderPath = string.Empty;
            correctionFilePath = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(folder))
            {
                error = "RTC6 파일 폴더가 입력되지 않았습니다.";
                return false;
            }

            try
            {
                folderPath = ResolvePath(folder);
            }
            catch (Exception exception)
            {
                error = "RTC6 폴더 경로를 처리할 수 없습니다.\n"
                    + exception.Message;

                return false;
            }

            if (!Directory.Exists(folderPath))
            {
                error = "RTC6 파일 폴더를 찾을 수 없습니다.\n"
                    + folderPath;

                return false;
            }

            string datFilePath = Path.Combine(
                folderPath,
                "RTC6DAT.dat");

            if (!File.Exists(datFilePath))
            {
                error = "RTC6DAT.dat 파일을 찾을 수 없습니다.\n"
                    + datFilePath;

                return false;
            }

            string outFilePath = Path.Combine(
                folderPath,
                "RTC6OUT.out");

            if (!File.Exists(outFilePath))
            {
                error = "RTC6OUT.out 파일을 찾을 수 없습니다.\n"
                    + outFilePath;

                return false;
            }

            if (string.IsNullOrWhiteSpace(correctionFileName))
            {
                error = "보정 파일명이 입력되지 않았습니다.";
                return false;
            }

            correctionFilePath = Path.Combine(
                folderPath,
                correctionFileName);

            if (!File.Exists(correctionFilePath))
            {
                error = correctionFileName + " 파일을 찾을 수 없습니다.\n"
                    + correctionFilePath;

                return false;
            }

            return true;
        }

        private static string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            return Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, path));
        }
    }
}