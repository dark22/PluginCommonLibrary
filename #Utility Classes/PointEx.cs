﻿// This file is provided unter the terms of the 
// Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
// To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/3.0/.
// 
// Written by CoderCow

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using DPoint = System.Drawing.Point;

namespace Terraria.Plugins.CoderCow {
  public static class PointEx {
    public static string ToSimpleString(this DPoint point) {
      return string.Concat(point.X, ',', point.Y);
    }

    public static DPoint Parse(string pointData) {
      string[] locationCoords = pointData.Split(',');
      if (locationCoords.Length != 2)
        throw new ArgumentException();

      return new DPoint(int.Parse(locationCoords[0]), int.Parse(locationCoords[1]));
    }
  }
}