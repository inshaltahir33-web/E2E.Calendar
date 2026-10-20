# UI ko professional banane ka workflow (vibe-coding band)

Problem: AI ko sirf "make week view" bolne se wo andaza lagata hai. Solution = **numbers + reference + feedback loop**.

1. **Spec do, adjective nahi**: "clean modern" mat likho; docs/08-DESIGN-SYSTEM.md ke numbers do.
2. **Ek prompt = ek component**. Month, Week, Sidebar ek saath mat banwao.
3. **Seed demo data pehle** (prompt U00) taake har view me events dikhen aur judge ho sake.
4. **Screenshot loop**: app chalao -> screenshot lo (Win+Shift+S) -> OpenCode me image attach karo ("compare with docs/08, list gaps, fix top 5") -> repeat. 3 rounds me ~80% polish.
5. **Reference app**: WinUI 3 Gallery (Store), Windows 11 Clock/Settings, new Outlook calendar side-by-side kholo.
6. **Check matrix har component ke baad**: Light/Dark, 100/125/150/200% scale, window 900px & 1600px, high-contrast, keyboard-only.
7. **Mockup pehle** (spec me bhi likha hai): bade redesigns ke liye pehle HTML/Figma mockup banao, phir code.
8. Vision-capable model use karo (OpenCode me screenshot samajhne wala model) warna loop kaam nahi karega.
9. XAML Hot Reload + Live Visual Tree (Visual Studio) se spacing/binding bugs seedha dekho.
