import re
import sys

helper_code = '''
        /// <summary>
        /// Safely retrieves TextService. Returns null if Core is not initialized (e.g., in unit tests).
        /// </summary>
        private TextService GetTextService()
        {
            if (_textService == null && Core.Services != null)
            {
                _textService = Core.Services.GetService<TextService>();
            }
            return _textService;
        }

        /// <summary>
        /// Gets localized text or falls back to key name if TextService unavailable.
        /// </summary>
        private string GetText(DialogueType type, TextKey key)
        {
            var service = GetTextService();
            return service?.DisplayText(type, key) ?? key.ToString();
        }
'''

for filepath in sys.argv[1:]:
    with open(filepath, 'r') as f:
        content = f.read()
    
    if 'private TextService GetTextService()' in content:
        continue
    
    # Remove GetService call
    content = re.sub(r'\s*_textService = Core\.Services\.GetService<TextService>\(\);\s*\n', '\n', content)
    
    # Find first method after constructor and add helpers before it
    # Look for pattern: "}\n\n        public " or "}\n\n        private "
    pattern = r'(\n        \})\n\n(        (?:public|private|internal|protected))'
    
    match = re.search(pattern, content)
    if match:
        content = content[:match.end(1)] + helper_code + '\n' + content[match.end(1)+2:]
        with open(filepath, 'w') as f:
            f.write(content)
        print(f"Fixed: {filepath}")
    else:
        print(f"Could not find insertion point: {filepath}")

