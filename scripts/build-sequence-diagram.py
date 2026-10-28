"""Draw C01's actual Blazor create/observe/send flow as a presentation-style UML image.

Writes src/SessionLab/wwwroot/architecture.svg and docs/images/architecture.svg.
With --png (requires `pip install playwright` and `playwright install chromium`),
also renders architecture.png next to both SVG copies.
"""
import sys
from html import escape
from pathlib import Path

APP = Path(__file__).resolve().parents[1]
W, H = 1560, 1880
LANE_TOP, LANE_END, STEP = 326, 1690, 53
p = [f'''<svg xmlns="http://www.w3.org/2000/svg" width="{W}" height="{H}" viewBox="0 0 {W} {H}" role="img" aria-labelledby="title description">
<title id="title">C01 (C# / Blazor) — Create a session and follow its first turn</title>
<desc id="description">UML sequence diagram with six participants: student, browser, the Home.razor component in the Blazor circuit, the SessionHub service, the Anthropic C# SDK, and Managed Agents. Twenty-five numbered messages show session creation, opening the provider stream before listing history, subscribing the component to the in-memory feed, sending a prompt, and rendering the result over SignalR. The architecture page provides the full text equivalent.</desc>
<defs>
<marker id="request" viewBox="0 0 12 12" refX="11" refY="6" markerWidth="10" markerHeight="10" orient="auto"><path d="M0 0 L12 6 L0 12 Z" fill="#193269"/></marker>
<marker id="response" viewBox="0 0 12 12" refX="11" refY="6" markerWidth="10" markerHeight="10" orient="auto"><path d="M1 1 L11 6 L1 11" fill="none" stroke="#193269" stroke-width="2"/></marker>
</defs>
<rect width="{W}" height="{H}" rx="26" fill="#fff"/>
<rect x="1" y="1" width="{W - 2}" height="{H - 2}" rx="26" fill="none" stroke="#d7e7f5" stroke-width="2"/>
<style>text{{font-family:Arial,Helvetica,sans-serif;fill:#090d3b}}</style>''']


def text(x, y, value, size=26, color='#193269', bold=False):
    p.append(f'<text x="{x}" y="{y}" font-size="{size}" font-weight="{"700" if bold else "400"}" fill="{color}" style="fill:{color}">{escape(value)}</text>')


def box(x, y, w, h, fill, stroke='#d7e7f5', r=16):
    p.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" stroke="{stroke}" stroke-width="2"/>')


def arrow(a, b, y, label, style='request'):
    dash = ' stroke-dasharray="9 7"' if style == 'return' else ''
    marker = 'request' if style == 'request' else 'response'
    p.append(f'<line x1="{a}" y1="{y}" x2="{b}" y2="{y}" stroke="#193269" stroke-width="2.5"{dash} marker-end="url(#{marker})"/>')
    if label:
        text(min(a, b) + 10, y - 11, label, 21)


text(34, 49, 'LESSON C01 (C#) / UML SEQUENCE DIAGRAM', 26, '#0872d4', True)
text(34, 104, 'Create a session. Observe its first turn.', 43, '#090d3b', True)
text(34, 148, 'Blazor Server application path • read from top to bottom', 28)

STUDENT, BROWSER, HOME, HUB, SDK, REMOTE = 130, 390, 650, 910, 1170, 1430
# Home.razor, SessionHub and the SDK all run inside one ASP.NET Core process.
box(HOME - 113, 176, SDK - HOME + 226, 43, '#f1eaff', r=9)
text(HOME - 100, 207, 'Host process: ASP.NET Core + Blazor Server + Anthropic SDK', 24, '#6534a2', True)
for x, title, detail, fill, accent in [
        (STUDENT, 'Student', 'Browser user', '#edf7ff', '#087dfa'),
        (BROWSER, 'Browser', 'blazor.web.js', '#edf7ff', '#087dfa'),
        (HOME, 'Home.razor', 'Blazor circuit', '#f3edff', '#853df2'),
        (HUB, 'SessionHub', 'Host service', '#f3edff', '#853df2'),
        (SDK, 'Anthropic SDK', 'AnthropicClient', '#f3edff', '#853df2'),
        (REMOTE, 'Managed Agents', 'Remote session', '#eaf8f1', '#00865d')]:
    box(x - 113, 232, 226, 94, fill)
    p.append(f'<rect x="{x - 113}" y="232" width="226" height="7" rx="3" fill="{accent}"/>')
    text(x - 100, 273, title, 25, '#090d3b', True)
    text(x - 100, 310, detail, 24)
    p.append(f'<line x1="{x}" y1="{LANE_TOP}" x2="{x}" y2="{LANE_END}" stroke="#97aac1" stroke-width="2" stroke-dasharray="7 7"/>')

messages = [
    (STUDENT, BROWSER, '1. New session', 'request'),
    (BROWSER, HOME, '2. Click (SignalR)', 'request'),
    (HOME, HUB, '3. CreateSessionAsync', 'request'),
    (HUB, SDK, '4. Sessions.Create', 'request'),
    (SDK, REMOTE, '5. Create request', 'request'),
    (REMOTE, SDK, '6. Session + ID', 'return'),
    (SDK, HUB, '7. Created session', 'return'),
    (HUB, HUB, '8. AttachAsync(id)', 'self'),
    (HUB, SDK, '9. StreamStreaming', 'request'),
    (SDK, REMOTE, '10. Open live stream', 'request'),
    (HUB, SDK, '11. Events.List', 'request'),
    (REMOTE, HUB, '12. History via SDK', 'return'),
    (HUB, HOME, '13. Session ID', 'return'),
    (HOME, HUB, '14. Subscribe', 'request'),
    (HUB, HOME, '15. Buffered events', 'event'),
    (HOME, BROWSER, '16. UI diff (SignalR)', 'event'),
    (STUDENT, BROWSER, '17. Send message', 'request'),
    (BROWSER, HOME, '18. Submit (SignalR)', 'request'),
    (HOME, HUB, '19. SendMessageAsync', 'request'),
    (HUB, SDK, '20. Events.Send', 'request'),
    (SDK, REMOTE, '21. user.message', 'request'),
    (REMOTE, HUB, '22. Agent + status events via SDK', 'event'),
    (HUB, HOME, '23. OnEvent → InvokeAsync', 'event'),
    (HOME, BROWSER, '24. UI diff (SignalR)', 'event'),
    (BROWSER, STUDENT, '25. Answer + state', 'return')]
for i, (a, b, label, style) in enumerate(messages):
    y = 381 + i * STEP
    if style == 'self':
        p.append(f'<path d="M {a} {y - 17} h 43 v 22 h -43" fill="none" stroke="#193269" stroke-width="2.5" marker-end="url(#request)"/>')
        text(a + 54, y - 1, label, 21)
    else:
        arrow(a, b, y, label, style)

box(27, 1712, W - 54, 61, '#eaf8f1', r=12)
text(45, 1752, 'Create once → open stream before history → subscribe the page → send after observation is ready.', 26, '#056b4b', True)
arrow(44, 106, 1826, '', style='request')
text(120, 1834, 'Request', 26)
arrow(325, 387, 1826, '', style='return')
text(400, 1834, 'Returned data', 26)
arrow(678, 740, 1826, '', style='event')
text(754, 1834, 'Event notification', 26)
p.append('</svg>')

svg = '\n'.join(p)
targets = [APP / 'src/SessionLab/wwwroot', APP / 'docs/images']
for folder in targets:
    folder.mkdir(parents=True, exist_ok=True)
    (folder / 'architecture.svg').write_text(svg, encoding='utf-8')
print(f'Generated architecture.svg: six lanes, {len(messages)} numbered messages.')

if '--png' in sys.argv:
    from playwright.sync_api import sync_playwright
    with sync_playwright() as pw:
        browser = pw.chromium.launch()
        page = browser.new_page(viewport={'width': W, 'height': H})
        page.set_content(f'<html><body style="margin:0">{svg}</body></html>')
        png = page.locator('svg').screenshot()
        browser.close()
    for folder in targets:
        (folder / 'architecture.png').write_bytes(png)
    print('Rendered architecture.png.')
