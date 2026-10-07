// Web Audio API Synthesizer for Restaurant POS Real-time Alerts
// Zero external asset dependencies - 100% reliable in all modern browsers

const SOUND_VOLUME_KEY = 'restaurant_pos_sound_volume';
const SOUND_ENABLED_KEY = 'restaurant_pos_sound_enabled';

export function getSoundVolume(): number {
  const stored = localStorage.getItem(SOUND_VOLUME_KEY);
  if (stored !== null) {
    const val = parseInt(stored, 10);
    if (!isNaN(val)) return Math.min(100, Math.max(0, val));
  }
  return 90; // Default 90%
}

export function setSoundVolume(volume: number): void {
  const clamped = Math.min(100, Math.max(0, volume));
  localStorage.setItem(SOUND_VOLUME_KEY, clamped.toString());
}

export function isSoundEnabled(): boolean {
  const stored = localStorage.getItem(SOUND_ENABLED_KEY);
  return stored !== 'false';
}

export function setSoundEnabled(enabled: boolean): void {
  localStorage.setItem(SOUND_ENABLED_KEY, enabled ? 'true' : 'false');
}

export function playOrderAlertChime() {
  if (!isSoundEnabled()) return;
  const volPercent = getSoundVolume();
  if (volPercent <= 0) return;

  try {
    const AudioContextClass = window.AudioContext || (window as any).webkitAudioContext;
    if (!AudioContextClass) return;
    const ctx = new AudioContextClass();

    if (ctx.state === 'suspended') {
      ctx.resume();
    }

    const now = ctx.currentTime;
    const baseGain = (volPercent / 100.0) * 0.85;

    // Rich dual-harmonic chime: (880Hz -> 1320Hz -> 1760Hz)
    const playTone = (freq: number, startTime: number, duration: number, gainMultiplier: number = 1.0) => {
      // Primary sine oscillator
      const osc1 = ctx.createOscillator();
      osc1.type = 'sine';
      osc1.frequency.setValueAtTime(freq, startTime);

      // Subtle harmonic overtone for presence and clarity in noisy kitchen
      const osc2 = ctx.createOscillator();
      osc2.type = 'triangle';
      osc2.frequency.setValueAtTime(freq * 2, startTime);

      const gain = ctx.createGain();
      const targetGain = baseGain * gainMultiplier;

      gain.gain.setValueAtTime(0.001, startTime);
      gain.gain.exponentialRampToValueAtTime(targetGain, startTime + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.001, startTime + duration);

      osc1.connect(gain);
      osc2.connect(gain);
      gain.connect(ctx.destination);

      osc1.start(startTime);
      osc2.start(startTime);
      osc1.stop(startTime + duration);
      osc2.stop(startTime + duration);
    };

    playTone(880, now, 0.20, 0.9);
    playTone(1320, now + 0.16, 0.26, 1.0);
    playTone(1760, now + 0.36, 0.50, 1.0);
  } catch (e) {
    console.warn('Sound alert warning:', e);
  }
}

export function playOrderReadyChime() {
  if (!isSoundEnabled()) return;
  const volPercent = getSoundVolume();
  if (volPercent <= 0) return;

  try {
    const AudioContextClass = window.AudioContext || (window as any).webkitAudioContext;
    if (!AudioContextClass) return;
    const ctx = new AudioContextClass();

    if (ctx.state === 'suspended') {
      ctx.resume();
    }

    const now = ctx.currentTime;
    const baseGain = (volPercent / 100.0) * 0.85;

    // Three rising notes for "Food Ready" (C5 -> E5 -> G5)
    const playTone = (freq: number, startTime: number, duration: number) => {
      const osc = ctx.createOscillator();
      const gain = ctx.createGain();
      osc.type = 'sine';
      osc.frequency.setValueAtTime(freq, startTime);
      gain.gain.setValueAtTime(0.001, startTime);
      gain.gain.exponentialRampToValueAtTime(baseGain, startTime + 0.02);
      gain.gain.exponentialRampToValueAtTime(0.001, startTime + duration);
      osc.connect(gain);
      gain.connect(ctx.destination);
      osc.start(startTime);
      osc.stop(startTime + duration);
    };

    playTone(523.25, now, 0.18);
    playTone(659.25, now + 0.15, 0.18);
    playTone(783.99, now + 0.32, 0.45);
  } catch (e) {
    console.warn('Sound alert warning:', e);
  }
}

export function testOrderAlertSound() {
  playOrderAlertChime();
}
