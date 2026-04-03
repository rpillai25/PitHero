#!/usr/bin/env python3
import sys
import re

def fix_file(filepath):
    with open(filepath, 'r') as f:
        content = f.read()
    
    # Pattern to match the GetService call in constructor
    pattern = r'(\s+)_textService = Core\.Services\.GetService<TextService>\(\);'
    
    # Check if the file already has GetTextService method (already fixed)
    if 'private TextService GetTextService()' in content:
        print(f"  Already fixed: {filepath}")
        return False
    
    # Check if the file has the pattern
    if not re.search(pattern, content):
        print(f"  Pattern not found: {filepath}")
        return False
    
    # Replace the GetService call with empty (remove it)
    content = re.sub(pattern, '', content)
    
    # Find the constructor end and add helper methods right after
    # Look for the closing brace of the constructor
    constructor_pattern = r'(public \w+\([^)]*\)\s*\{[^}]*)\}'
    
    helper_methods = '''
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
    
    # More sophisticated approach: find constructor and add methods after it
    # This is tricky, so let's do it manually for each file
    
    print(f"  Needs manual fix: {filepath}")
    return True

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: fix_textservice_constructor.py <file1> [file2] ...")
        sys.exit(1)
    
    for filepath in sys.argv[1:]:
        print(f"Checking {filepath}...")
        fix_file(filepath)
