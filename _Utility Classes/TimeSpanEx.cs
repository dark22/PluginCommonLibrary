﻿// This file is provided unter the terms of the 
// Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/3.0/.
// 
// Written by CoderCow

using System;
using System.Diagnostics.Contracts;
using System.Text;
using System.Text.RegularExpressions;

namespace Terraria.Plugins.CoderCow {
  public static class TimeSpanEx {
    #region [Method: Static TryParseShort]
    // Group identifiers: Days, Hours, Minutes, Seconds
    private static readonly Regex parseShortTimeSpanRegex = new Regex(
      @"^(\W*((?<Days>\d+)\W*d(ays)?)?\W*((?<Hours>\d+)\W*h(ours|rs)?)?\W*((?<Minutes>\d+)\W*m(inutes|ins)?)?\W*((?<Seconds>\d+)\W*s(econds|ecs)?)?)$",
      RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture | RegexOptions.IgnorePatternWhitespace
    );

    public static bool TryParseShort(string input, out TimeSpan result) {
      Contract.Requires<ArgumentNullException>(input != null);

      result = TimeSpan.Zero;
      if (string.IsNullOrWhiteSpace(input))
        return false;

      Match regexMatch = TimeSpanEx.parseShortTimeSpanRegex.Match(input);
      if (!regexMatch.Success)
        return false;

      Group daysGroup = regexMatch.Groups["Days"];
      if (daysGroup != null) {
        int days;
        if (!int.TryParse(daysGroup.Value, out days))
          return false;

        result += TimeSpan.FromDays(days);
      }
      Group hoursGroup = regexMatch.Groups["Hours"];
      if (hoursGroup != null) {
        int hours;
        if (!int.TryParse(hoursGroup.Value, out hours))
          return false;

        result += TimeSpan.FromHours(hours);
      }

      Group minutesGroup = regexMatch.Groups["Minutes"];
      if (minutesGroup != null) {
        int minutes;
        if (!int.TryParse(minutesGroup.Value, out minutes))
          return false;

        result += TimeSpan.FromMinutes(minutes);
      }

      Group secondsGroup = regexMatch.Groups["Seconds"];
      if (secondsGroup != null) {
        int seconds;
        if (!int.TryParse(secondsGroup.Value, out seconds))
          return false;

        result += TimeSpan.FromSeconds(seconds);
      }

      return true;
    }
    #endregion

    #region [Method: Static ToLongString]
    public static string ToLongString(this TimeSpan timeSpan) {
      StringBuilder result = new StringBuilder();
      int totalDays = (int)timeSpan.TotalDays;
      if (totalDays == 1) {
        result.Append("1 day");
      } else if (totalDays > 0) {
        result.Append(timeSpan.TotalDays);
        result.Append(" days");
      }

      if (timeSpan.Hours == 1) {
        if (result.Length > 0)
          result.Append(' ');

        result.Append("1 hour");
      } else if (timeSpan.Hours > 0) {
        if (result.Length > 0)
          result.Append(' ');

        result.Append(timeSpan.Hours);
        result.Append(" hours");
      }

      if (timeSpan.Minutes == 1) {
        if (result.Length > 0)
          result.Append(' ');

        result.Append("1 minute");
      } else if (timeSpan.Minutes > 0) {
        if (result.Length > 0)
          result.Append(' ');

        result.Append(timeSpan.Minutes);
        result.Append(" minutes");
      }

      return result.ToString();
    }
    #endregion
  }
}