import re
import sys

# Set UTF-8 encoding for output
sys.stdout.reconfigure(encoding='utf-8')

# Read the ExerciseTextService.cs file
file_path = r"C:\Users\asarayja\Desktop\FemVoice Studio\FemVoiceStudio\Services\ExerciseTextService.cs"

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Find all exercise blocks
pattern = r'texts\.Add\(new ExerciseText\s*\{(.*?)\}\);'
matches = re.findall(pattern, content, re.DOTALL)

exercises = []
for match in matches:
    id_match = re.search(r'Id\s*=\s*(\d+)', match)
    title_match = re.search(r'Title\s*=\s*"([^"]+)"', match)
    content_match = re.search(r'Content\s*=\s*"([^"]+)"', match)
    desc_match = re.search(r'Description\s*=\s*"([^"]+)"', match)
    cat_match = re.search(r'Category\s*=\s*"([^"]+)"', match)
    
    if id_match and title_match and content_match:
        ex = {
            'id': id_match.group(1),
            'title': title_match.group(1),
            'content': content_match.group(1),
            'description': desc_match.group(1) if desc_match else "",
            'category': cat_match.group(1) if cat_match else ""
        }
        exercises.append(ex)

# Sort by ID
exercises.sort(key=lambda x: int(x['id']))

# Determine difficulty based on ID
for ex in exercises:
    ex_id = int(ex['id'])
    if ex_id <= 24:
        ex['difficulty'] = "Nybegynner"
    elif ex_id <= 50:
        ex['difficulty'] = "Middels"  
    else:
        ex['difficulty'] = "Avansert"

print(f"Found {len(exercises)} exercises")

# Now generate complete resource file content
# First generate Norwegian (Strings.resx)

output = '''<?xml version="1.0" encoding="utf-8"?>
<root>
  <xsd:schema id="root" xmlns="" xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata">
    <xsd:element name="root" msdata:IsDataSet="true">
      <xsd:complexType>
        <xsd:choice maxOccurs="unbounded">
          <xsd:element name="data">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
                <xsd:element name="comment" type="xsd:string" minOccurs="0" msdata:Ordinal="2" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" msdata:Ordinal="0" />
              <xsd:attribute name="type" type="xsd:string" msdata:Ordinal="3" />
              <xsd:attribute name="mimetype" type="xsd:string" msdata:Ordinal="4" />
            </xsd:complexType>
          </xsd:element>
          <xsd:element name="resheader">
            <xsd:complexType>
              <xsd:sequence>
                <xsd:element name="value" type="xsd:string" minOccurs="0" msdata:Ordinal="1" />
              </xsd:sequence>
              <xsd:attribute name="name" type="xsd:string" use="required" />
            </xsd:complexType>
          </xsd:element>
        </xsd:choice>
      </xsd:complexType>
    </xsd:element>
  </xsd:schema>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>
  </resheader>
'''

# Add existing entries (I'll add just the exercise texts)
for ex in exercises:
    output += f'''
  <!-- Exercise {ex['id']} - {ex['difficulty']} -->
  <data name="Exercise_{ex['id']}_Title" xml:space="preserve">
    <value>{ex['title']}</value>
  </data>
  <data name="Exercise_{ex['id']}_Content" xml:space="preserve">
    <value>{ex['content']}</value>
  </data>
  <data name="Exercise_{ex['id']}_Description" xml:space="preserve">
    <value>{ex['description']}</value>
  </data>
  <data name="Exercise_{ex['id']}_Category" xml:space="preserve">
    <value>{ex['category']}</value>
  </data>
'''

output += '''
</root>
'''

# Save Norwegian file
norwegian_path = r"C:\Users\asarayja\Desktop\FemVoice Studio\FemVoiceStudio\Resources\Strings.resx"
# Read original to get existing content
with open(norwegian_path, 'r', encoding='utf-8') as f:
    original = f.read()

# Find where to insert (after </resheader> and before first data element)
import xml.etree.ElementTree as ET

# Parse the original file to preserve existing entries
tree = ET.parse(norwegian_path)
root = tree.getroot()

# Add new exercise entries
ns = {'ns': 'urn:schemas-microsoft-com:xml-msdata'}

for ex in exercises:
    for field in ['Title', 'Content', 'Description', 'Category']:
        data = ET.SubElement(root, 'data')
        data.set('name', f"Exercise_{ex['id']}_{field}")
        data.set('xml:space', 'preserve')
        value = ET.SubElement(data, 'value')
        value.text = ex[field.lower()]

# Save
tree.write(norwegian_path, encoding='utf-8', xml_declaration=True)

print(f"Updated Norwegian resource file with {len(exercises)} exercises")
print("Done!")
