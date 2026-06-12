using System.Text;
using UnityEngine.UI;

namespace Runefall.Presentation.Combat
{
    /// <summary>
    /// Rolling combat log. Appends lines, caps the buffer at a fixed number of
    /// rows and renders into a wired <see cref="Text"/>. Single responsibility:
    /// the text feed shown during battle (no knowledge of cards, slots or turns).
    /// </summary>
    public sealed class CombatLogView
    {
        private const int k_MaxLines = 22;

        private readonly Text          _output;
        private readonly StringBuilder _buffer = new();

        public CombatLogView(Text output) => _output = output;

        public void Append(string line)
        {
            _buffer.AppendLine(line);

            var lines = _buffer.ToString().Split('\n');
            if (lines.Length > k_MaxLines)
            {
                _buffer.Clear();
                for (int i = lines.Length - (k_MaxLines - 1); i < lines.Length; i++)
                    _buffer.AppendLine(lines[i]);
            }

            if (_output != null) _output.text = _buffer.ToString();
        }
    }
}
