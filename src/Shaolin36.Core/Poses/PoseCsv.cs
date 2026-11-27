using System;
using System.Collections.Generic;
using System.Globalization;
using Shaolin36.Core.Data;

namespace Shaolin36.Core.Poses
{
    /// <summary>
    /// Reads the pose sheet: one row per joint of a pose, "pose,joint,pitch,yaw,roll" in whole degrees.
    /// Joints a pose leaves out stay at 0. Bad rows are reported, never silently loaded.
    /// </summary>
    public static class PoseCsv
    {
        public static readonly string[] Columns = { "pose", "joint", "pitch", "yaw", "roll" };

        public sealed class Result
        {
            public Result(PoseSheet? sheet, List<string> errors)
            {
                Sheet = sheet;
                Errors = errors;
            }

            public PoseSheet? Sheet { get; }

            /// <summary>Human-readable problems, each starting with "line N:".</summary>
            public List<string> Errors { get; }

            public bool Ok => Errors.Count == 0;
        }

        public static Result Parse(string text)
        {
            var errors = new List<string>();
            var lines = CsvText.SplitLines(text);
            int headerIndex = -1;
            for (int i = 0; i < lines.Count; i++)
                if (!CsvText.IsBlankOrComment(lines[i])) { headerIndex = i; break; }
            if (headerIndex < 0)
            {
                errors.Add("line 1: file is empty, expected a header row");
                return new Result(null, errors);
            }

            var header = CsvText.SplitRow(lines[headerIndex]);
            var col = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 0; c < header.Count; c++) col[header[c].Trim()] = c;
            foreach (var name in Columns)
                if (!col.ContainsKey(name)) errors.Add($"line {headerIndex + 1}: missing column '{name}'");
            if (errors.Count > 0) return new Result(null, errors);

            var poses = new Dictionary<string, Pose>(StringComparer.OrdinalIgnoreCase);
            var order = new List<Pose>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = headerIndex + 1; i < lines.Count; i++)
            {
                if (CsvText.IsBlankOrComment(lines[i])) continue;
                int lineNo = i + 1;
                var cells = CsvText.SplitRow(lines[i]);
                var rowErrors = new List<string>();

                string Cell(string name)
                {
                    int idx = col[name];
                    return idx < cells.Count ? cells[idx].Trim() : string.Empty;
                }

                int Angle(string name)
                {
                    var raw = Cell(name);
                    if (raw.Length == 0) return 0;
                    if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && Pose.InRange(v)) return v;
                    rowErrors.Add($"'{name}' must be a whole number of degrees from -{Pose.MaxAngle} to {Pose.MaxAngle}, got '{raw}'");
                    return 0;
                }

                var poseName = Cell("pose");
                if (poseName.Length == 0) rowErrors.Add("'pose' is empty");
                var jointName = Cell("joint");
                if (!Joints.TryParse(jointName, out var joint))
                {
                    var known = new List<string>();
                    for (int j = 0; j < Joints.Count; j++) known.Add(Joints.Name((Joint)j));
                    rowErrors.Add($"unknown joint '{jointName}' ({string.Join(", ", known)})");
                }
                int pitch = Angle("pitch"), yaw = Angle("yaw"), roll = Angle("roll");
                if (rowErrors.Count == 0 && !seen.Add(poseName + "|" + Joints.Name(joint)))
                    rowErrors.Add($"pose '{poseName}' sets '{Joints.Name(joint)}' twice");

                if (rowErrors.Count > 0)
                {
                    foreach (var e in rowErrors) errors.Add($"line {lineNo}: {e}");
                    continue;
                }
                if (!poses.TryGetValue(poseName, out var pose))
                {
                    pose = new Pose(poseName);
                    poses[poseName] = pose;
                    order.Add(pose);
                }
                pose.Set(joint, pitch, yaw, roll);
            }
            return new Result(errors.Count == 0 ? new PoseSheet(order) : null, errors);
        }
    }
}
