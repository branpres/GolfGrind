(() => {
    const animations = new WeakMap();

    function cancel(svg) {
        const frame = animations.get(svg);
        if (frame !== undefined) {
            cancelAnimationFrame(frame);
            animations.delete(svg);
        }
    }

    function animate(svg, durationMs) {
        if (!svg) return;
        cancel(svg);

        const root = svg.querySelector(".tracer-animation");
        const path = root?.querySelector(".animated-shot-path");
        const ball = root?.querySelector(".shot-ball");
        const landing = root?.querySelector(".animated-shot-end");
        if (!path || !ball || !landing) return;

        const length = path.getTotalLength();
        if (!Number.isFinite(length) || length <= 0) return;

        path.style.strokeDasharray = `${length}`;
        path.style.strokeDashoffset = `${length}`;
        path.style.visibility = "visible";
        ball.style.opacity = "1";
        landing.style.opacity = "0";

        const finish = () => {
            const point = path.getPointAtLength(length);
            path.style.strokeDashoffset = "0";
            ball.setAttribute("transform", `translate(${point.x} ${point.y})`);
            ball.style.opacity = "0";
            landing.style.opacity = "1";
            animations.delete(svg);
        };

        if (matchMedia("(prefers-reduced-motion: reduce)").matches) {
            finish();
            return;
        }

        const duration = Math.max(250, Number(durationMs) || 3500);
        const startPoint = path.getPointAtLength(0);
        ball.setAttribute("transform", `translate(${startPoint.x} ${startPoint.y})`);
        const startedAt = performance.now();

        const step = now => {
            const progress = Math.min(1, (now - startedAt) / duration);
            const travelled = length * progress;
            const point = path.getPointAtLength(travelled);
            path.style.strokeDashoffset = `${length - travelled}`;
            ball.setAttribute("transform", `translate(${point.x} ${point.y})`);

            if (progress < 1) {
                animations.set(svg, requestAnimationFrame(step));
            } else {
                finish();
            }
        };

        animations.set(svg, requestAnimationFrame(step));
    }

    window.golfTracer = { animate, cancel };

    let readyAudioContext;
    async function playReadyChime() {
        const AudioContext = window.AudioContext || window.webkitAudioContext;
        if (!AudioContext) return;

        readyAudioContext ??= new AudioContext();
        if (readyAudioContext.state === "suspended")
            await readyAudioContext.resume();

        const startedAt = readyAudioContext.currentTime;
        const gain = readyAudioContext.createGain();
        gain.gain.setValueAtTime(0.0001, startedAt);
        gain.gain.exponentialRampToValueAtTime(0.16, startedAt + 0.015);
        gain.gain.exponentialRampToValueAtTime(0.0001, startedAt + 0.55);
        gain.connect(readyAudioContext.destination);

        for (const [frequency, level] of [[880, 1], [1320, 0.35]]) {
            const oscillator = readyAudioContext.createOscillator();
            const partialGain = readyAudioContext.createGain();
            oscillator.type = "sine";
            oscillator.frequency.setValueAtTime(frequency, startedAt);
            partialGain.gain.setValueAtTime(level, startedAt);
            oscillator.connect(partialGain);
            partialGain.connect(gain);
            oscillator.start(startedAt);
            oscillator.stop(startedAt + 0.56);
        }
    }

    window.golfReadyChime = { play: playReadyChime };
})();
