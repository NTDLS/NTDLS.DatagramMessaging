﻿using System.Text.RegularExpressions;

namespace NTDLS.DatagramMessaging.Framing
{
    internal partial class CompiledRegEx
    {
        [GeneratedRegex(@"(,?\s*Version\s*=\s*[\d.]+)|(,?\s*Culture\s*=\s*[^,]+)|(,?\s*PublicKeyToken\s*=\s*[^,\]]+)")]
        internal static partial Regex TypeTagsRegex();

        [GeneratedRegex(@"\s*,\s*")]
        internal static partial Regex TypeCleanupRegex();
    }
}