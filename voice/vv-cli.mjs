// vv-cli.mjs - VOICEVOX 本地合成 CLI（BigFishPet 桌宠语音用，本地引擎不走网络）
// 用法: (echo 文本 | ) node vv-cli.mjs <speakerId> <output.wav>
// 文本从 stdin 读取; 引擎地址默认 http://127.0.0.1:50021, 可用环境变量 VV_API 覆盖
let text = '';
process.stdin.setEncoding('utf8');
process.stdin.on('data', (d) => { text += d; });
process.stdin.on('end', async () => {
  const [speaker, out] = process.argv.slice(2);
  if (!speaker || !out || !text.trim()) {
    console.error('[vv] usage: echo TEXT | node vv-cli.mjs <speakerId> <out.wav>');
    process.exit(2);
  }
  const api = process.env.VV_API || 'http://127.0.0.1:50021';
  try {
    const q = await fetch(api + '/audio_query?text=' + encodeURIComponent(text.trim()) + '&speaker=' + speaker, { method: 'POST' })
      .then(r => { if (!r.ok) throw new Error('audio_query HTTP ' + r.status); return r.json(); });
    const wav = await fetch(api + '/synthesis?speaker=' + speaker, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(q) })
      .then(r => { if (!r.ok) throw new Error('synthesis HTTP ' + r.status); return r.arrayBuffer(); });
    const { writeFileSync } = await import('node:fs');
    writeFileSync(out, Buffer.from(wav));
    console.log('[OK] ' + out + ' (' + wav.byteLength + 'B)');
  } catch (e) {
    console.error('[vv] ' + (e && e.message ? e.message : String(e)));
    process.exit(1);
  }
});
