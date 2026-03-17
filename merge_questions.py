import json

old = json.load(open('questions.json', 'r', encoding='utf-8'))
new = json.load(open('new_questions_500.json', 'r', encoding='utf-8'))[:500]

# Renumber new questions
for i, q in enumerate(new):
    q['Id'] = 302 + i

merged = old + new
json.dump(merged, open('questions.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=2)

print(f"Merged: {len(old)} + {len(new)} = {len(merged)} questions")

ili = [q for q in new if ' или ' in q['Text'].lower()]
print(f"Questions with 'или': {len(ili)}")
for q in ili[:10]:
    print(f"  {q['Id']}: {q['Text']} -> {q['Answer']}")
