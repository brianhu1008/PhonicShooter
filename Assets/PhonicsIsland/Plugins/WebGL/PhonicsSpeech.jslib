// Browser text-to-speech for the web version (called from PhonicsAudio.cs).
mergeInto(LibraryManager.library, {
  PhonicsSpeak: function (ptr) {
    var text = UTF8ToString(ptr);
    var synth = window.speechSynthesis;
    if (!synth) return;
    // Prefer natural-sounding English voices (Edge "Natural", Google), then any US English voice.
    var voices = synth.getVoices();
    var best = null, score = -1;
    for (var i = 0; i < voices.length; i++) {
      var v = voices[i];
      if (!v.lang || v.lang.toLowerCase().indexOf('en') !== 0) continue;
      var s = 1;
      if (v.lang.toLowerCase() === 'en-us') s += 2;
      if (/natural/i.test(v.name)) s += 4;
      if (/google/i.test(v.name)) s += 3;
      if (s > score) { score = s; best = v; }
    }
    synth.cancel();
    var u = new SpeechSynthesisUtterance(text);
    u.lang = 'en-US';
    u.rate = 0.8;
    if (best) u.voice = best;
    synth.speak(u);
  }
});
