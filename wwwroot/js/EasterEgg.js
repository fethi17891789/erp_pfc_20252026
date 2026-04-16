/**
 * SKYRA Easter Egg — Konami Code
 * ↑ ↑ ↓ ↓ ← → ← → B A
 */
(function () {
    const KONAMI = [
        'ArrowUp','ArrowUp',
        'ArrowDown','ArrowDown',
        'ArrowLeft','ArrowRight',
        'ArrowLeft','ArrowRight',
        'b','a'
    ];
    let position = 0;

    document.addEventListener('keydown', function (e) {
        if (e.key === KONAMI[position]) {
            position++;
            if (position === KONAMI.length) {
                position = 0;
                lancerEasterEgg();
            }
        } else {
            position = e.key === KONAMI[0] ? 1 : 0;
        }
    });

    function lancerEasterEgg() {
        if (document.getElementById('skyra-easter-egg')) return;

        const overlay = document.createElement('div');
        overlay.id = 'skyra-easter-egg';
        overlay.innerHTML = `
            <div class="ee-backdrop"></div>
            <div class="ee-card">
                <div class="ee-glitch" data-text="CHEAT ACTIVATED">CHEAT ACTIVATED</div>
                <div class="ee-code">↑ ↑ ↓ ↓ ← → ← → B A</div>
                <div class="ee-avatar">🎮</div>
                <div class="ee-title">Konami Code</div>
                <div class="ee-subtitle">+30 vies accordées.<br>Bonne chance pour la soutenance.</div>
                <div class="ee-stats">
                    <div class="ee-stat"><span class="ee-stat-val">∞</span><span class="ee-stat-label">Café</span></div>
                    <div class="ee-stat"><span class="ee-stat-val">+30</span><span class="ee-stat-label">Vies</span></div>
                    <div class="ee-stat"><span class="ee-stat-val">100%</span><span class="ee-stat-label">Swag</span></div>
                </div>
                <button class="ee-close" onclick="document.getElementById('skyra-easter-egg').remove()">Continuer →</button>
            </div>
        `;

        const style = document.createElement('style');
        style.id = 'skyra-easter-egg-style';
        style.textContent = `
            #skyra-easter-egg {
                position: fixed;
                inset: 0;
                z-index: 99999;
                display: flex;
                align-items: center;
                justify-content: center;
                animation: ee-fadein 0.3s ease;
            }
            @keyframes ee-fadein {
                from { opacity: 0; }
                to   { opacity: 1; }
            }
            .ee-backdrop {
                position: absolute;
                inset: 0;
                background: rgba(2, 3, 10, 0.92);
                backdrop-filter: blur(12px);
            }
            .ee-card {
                position: relative;
                background: linear-gradient(135deg, rgba(123,94,255,0.15), rgba(5,7,24,0.95));
                border: 1px solid rgba(123,94,255,0.45);
                border-radius: 24px;
                padding: 48px 56px;
                text-align: center;
                max-width: 460px;
                width: 90%;
                box-shadow: 0 0 80px rgba(123,94,255,0.3), 0 0 160px rgba(123,94,255,0.1);
                animation: ee-slidein 0.4s cubic-bezier(0.34,1.56,0.64,1);
            }
            @keyframes ee-slidein {
                from { transform: scale(0.7) translateY(40px); opacity: 0; }
                to   { transform: scale(1) translateY(0);     opacity: 1; }
            }
            .ee-glitch {
                font-family: 'Plus Jakarta Sans', sans-serif;
                font-size: 28px;
                font-weight: 800;
                letter-spacing: 4px;
                color: #fff;
                position: relative;
                display: inline-block;
                margin-bottom: 8px;
                text-transform: uppercase;
                animation: ee-glow 2s ease-in-out infinite alternate;
            }
            .ee-glitch::before,
            .ee-glitch::after {
                content: attr(data-text);
                position: absolute;
                inset: 0;
                background: transparent;
            }
            .ee-glitch::before {
                color: #7B5EFF;
                clip: rect(0, 900px, 0, 0);
                animation: ee-glitch1 2.5s infinite linear alternate-reverse;
            }
            .ee-glitch::after {
                color: #ff5e9c;
                clip: rect(0, 900px, 0, 0);
                animation: ee-glitch2 2s infinite linear alternate-reverse;
            }
            @keyframes ee-glitch1 {
                0%   { clip: rect(42px,9999px,44px,0); transform: skew(0.4deg); }
                20%  { clip: rect(12px,9999px,59px,0); transform: skew(0.1deg); }
                40%  { clip: rect(66px,9999px,30px,0); transform: skew(0.6deg); }
                60%  { clip: rect(8px, 9999px,72px,0); transform: skew(0.2deg); }
                80%  { clip: rect(50px,9999px,18px,0); transform: skew(0.5deg); }
                100% { clip: rect(3px, 9999px,85px,0); transform: skew(0.1deg); }
            }
            @keyframes ee-glitch2 {
                0%   { clip: rect(65px,9999px,20px,0); transform: skew(-0.3deg); }
                25%  { clip: rect(30px,9999px,55px,0); transform: skew(-0.5deg); }
                50%  { clip: rect(10px,9999px,40px,0); transform: skew(-0.1deg); }
                75%  { clip: rect(78px,9999px,12px,0); transform: skew(-0.4deg); }
                100% { clip: rect(25px,9999px,68px,0); transform: skew(-0.2deg); }
            }
            @keyframes ee-glow {
                from { text-shadow: 0 0 10px rgba(123,94,255,0.5); }
                to   { text-shadow: 0 0 30px rgba(123,94,255,0.9), 0 0 60px rgba(123,94,255,0.4); }
            }
            .ee-code {
                font-family: monospace;
                font-size: 13px;
                color: #7B5EFF;
                letter-spacing: 3px;
                margin-bottom: 28px;
                opacity: 0.9;
            }
            .ee-avatar {
                font-size: 64px;
                line-height: 1;
                margin-bottom: 16px;
                animation: ee-bounce 1s ease infinite alternate;
            }
            @keyframes ee-bounce {
                from { transform: translateY(0);   }
                to   { transform: translateY(-8px); }
            }
            .ee-title {
                font-family: 'Plus Jakarta Sans', sans-serif;
                font-size: 20px;
                font-weight: 700;
                color: #fff;
                margin-bottom: 8px;
            }
            .ee-subtitle {
                font-family: 'Plus Jakarta Sans', sans-serif;
                font-size: 14px;
                color: #A4A7C8;
                line-height: 1.6;
                margin-bottom: 32px;
            }
            .ee-stats {
                display: flex;
                justify-content: center;
                gap: 32px;
                margin-bottom: 36px;
                padding: 20px;
                background: rgba(123,94,255,0.08);
                border-radius: 16px;
                border: 1px solid rgba(123,94,255,0.2);
            }
            .ee-stat {
                display: flex;
                flex-direction: column;
                align-items: center;
                gap: 4px;
            }
            .ee-stat-val {
                font-family: 'Plus Jakarta Sans', sans-serif;
                font-size: 22px;
                font-weight: 800;
                color: #9C8CFF;
            }
            .ee-stat-label {
                font-family: 'Plus Jakarta Sans', sans-serif;
                font-size: 11px;
                color: #7F83A5;
                text-transform: uppercase;
                letter-spacing: 1px;
            }
            .ee-close {
                background: linear-gradient(135deg, #7B5EFF, #9C8CFF);
                color: #fff;
                border: none;
                border-radius: 999px;
                padding: 12px 36px;
                font-family: 'Plus Jakarta Sans', sans-serif;
                font-size: 15px;
                font-weight: 700;
                cursor: pointer;
                letter-spacing: 0.5px;
                transition: transform 0.2s, box-shadow 0.2s;
                box-shadow: 0 4px 24px rgba(123,94,255,0.4);
            }
            .ee-close:hover {
                transform: translateY(-2px);
                box-shadow: 0 8px 32px rgba(123,94,255,0.6);
            }
        `;

        document.head.appendChild(style);
        document.body.appendChild(overlay);

        overlay.querySelector('.ee-backdrop').addEventListener('click', function () {
            overlay.remove();
            document.getElementById('skyra-easter-egg-style')?.remove();
        });
    }
})();
