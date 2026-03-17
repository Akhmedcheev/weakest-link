import requests, json, time

key = 'AIzaSyA_6_70pufshcRrA9xAK79q6YL-fQXiy7Q'
url = f'https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={key}'
results = []

for batch in range(4):
    sid = 51 + batch * 50
    prompt = (
        f"Сгенерируй ровно 50 вопросов для ФИНАЛА телевикторины Слабое Звено. "
        f"Финальные вопросы должны быть СЛОЖНЕЕ обычных. Темы: история, наука, литература, "
        f"география, искусство, кино, музыка, мифология, спорт, технологии. "
        f"10 из них - вопросы с выбором через ИЛИ (пример: Платон или Аристотель - кто был учителем Александра Македонского?). "
        f"Вопросы короткие (1 предложение). Ответы 1-3 слова. "
        f'JSON массив: [{{"Id":{sid},"Text":"вопрос?","Answer":"ответ"}}]. '
        f"Начни с Id {sid}. ТОЛЬКО JSON без markdown."
    )
    body = {
        "contents": [{"parts": [{"text": prompt}]}],
        "generationConfig": {"temperature": 0.9, "maxOutputTokens": 8000}
    }
    for attempt in range(3):
        try:
            r = requests.post(url, json=body, timeout=90)
            if r.status_code != 200:
                print(f"  Batch {batch} attempt {attempt}: HTTP {r.status_code}")
                time.sleep(5)
                continue
            txt = r.json()["candidates"][0]["content"]["parts"][0]["text"].strip()
            if txt.startswith("```"):
                txt = txt.split("\n", 1)[1]
            if txt.endswith("```"):
                txt = txt.rsplit("\n", 1)[0]
            qs = json.loads(txt)
            results.extend(qs)
            print(f"Batch {batch}: +{len(qs)} (total {len(results)})")
            time.sleep(2)
            break
        except Exception as ex:
            print(f"  Batch {batch} attempt {attempt}: {str(ex)[:80]}")
            time.sleep(3)

# Load existing and merge
old = json.load(open('final_questions.json', 'r', encoding='utf-8'))
for i, q in enumerate(results):
    q['Id'] = 51 + i

merged = old + results
json.dump(merged, open('final_questions.json', 'w', encoding='utf-8'), ensure_ascii=False, indent=2)

ili = [q for q in results if ' или ' in q.get('Text', '').lower()]
print(f"\nMerged: {len(old)} + {len(results)} = {len(merged)} final questions")
print(f"With 'или': {len(ili)}")
for q in ili[:5]:
    print(f"  {q['Id']}: {q['Text']} -> {q['Answer']}")
