// tts-cli.mjs - edge-tts 命令行封装（BigFishPet 桌宠语音用）
// 用法: (echo 文本 | ) node tts-cli.mjs <voice> <output.mp3>
// 文本从 stdin 读取(避免命令行转义问题); 模块定位: env EDGE_TTS_MODULE -> dsh 默认位置
let text = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (d) => { text += d; });
process.stdin.on('end', async () => {
  const [voice, out] = process.argv.slice(2);
  if (!voice || !out || !text.trim()) {
    console.error('[tts] usage: echo TEXT | node tts-cli.mjs <voice> <out.mp3>');
    process.exit(2);
  }
  const candidates = [
    process.env.EDGE_TTS_MODULE,
    'C:/Users/21589/.dsh/profiles/qqbot/node_modules/dsh-voice/lib/edge-tts.js'
  ].filter(Boolean);
  let mod = null;
  for (const c of candidates) {
    try {
      const url = 'file:///' + String(c).replace(/\\/g, '/').replace(/^\/+/, '');
      mod = await import(url);
      break;
    } catch { /* try next candidate */ }
  }
  if (!mod) { console.error('[tts] edge-tts module not found (set EDGE_TTS_MODULE)'); process.exit(3); }
  try {
    const audio = await mod.synthesizeSpeech({ voice, text: text.trim(), rate: '+0%', pitch: '+0Hz' });
    const { writeFileSync } = await import('node:fs');
    writeFileSync(out, audio);
    console.log('[OK] ' + out + ' (' + audio.length + 'B)');
  } catch (e) {
    console.error('[tts] ' + (e && e.message ? e.message : String(e)));
    process.exit(1);
  }
});
