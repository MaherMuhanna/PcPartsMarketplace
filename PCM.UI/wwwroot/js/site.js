// Small site-wide helpers. Bootstrap's bundle is loaded before this file.
(function () {
    'use strict';

    // Flash messages (_Toasts partial): show every toast rendered by the server.
    document.querySelectorAll('.site-toasts .toast').forEach(function (element) {
        bootstrap.Toast.getOrCreateInstance(element).show();
    });

    // <form data-confirm="Question?"> asks before submitting (delete buttons and similar).
    document.addEventListener('submit', function (event) {
        var form = event.target;
        if (form instanceof HTMLFormElement && form.dataset.confirm && !window.confirm(form.dataset.confirm)) {
            event.preventDefault();
        }
    });

    // Price inputs (<input data-price-input data-min data-max> inside an .input-group with
    // [data-step-by] buttons): the buttons and the Up/Down keys move the price by whole JOD and keep
    // the cents (9.75 -> 10.75), clamped to the allowed range. The browser's own number spinner is not
    // used because it snaps decimal values to its step grid in browser-specific ways.
    var arabicDigits = /[\u0660-\u0669\u06F0-\u06F9]/g;

    // Arabic-Indic digits and the Arabic decimal separator typed on an Arabic keyboard become 0-9 and '.'.
    function toLatinNumber(text) {
        return text
            .replace(arabicDigits, function (digit) {
                var code = digit.charCodeAt(0);
                return String(code - (code >= 0x06F0 ? 0x06F0 : 0x0660));
            })
            .replace(/\u066B/g, '.');
    }

    document.querySelectorAll('input[data-price-input]').forEach(function (input) {
        var min = parseFloat(input.dataset.min || '0.01');
        var max = parseFloat(input.dataset.max || '999999.99');

        function stepBy(delta) {
            var current = parseFloat(toLatinNumber(input.value));
            var next = isFinite(current) ? current + delta : (delta > 0 ? 1 : min);
            next = Math.round(next * 100) / 100;            // avoid 10.749999 from floating point
            next = Math.min(max, Math.max(min, next));
            input.value = next.toFixed(2);
            input.dispatchEvent(new Event('change', { bubbles: true }));
        }

        input.addEventListener('input', function () {
            var latin = toLatinNumber(input.value);
            if (latin !== input.value) {
                input.value = latin;
            }
        });

        input.addEventListener('keydown', function (event) {
            if (event.key === 'ArrowUp' || event.key === 'ArrowDown') {
                event.preventDefault();
                stepBy(event.key === 'ArrowUp' ? 1 : -1);
            }
        });

        var group = input.closest('.input-group');
        if (group) {
            group.querySelectorAll('[data-step-by]').forEach(function (button) {
                button.addEventListener('click', function () {
                    stepBy(Number(button.dataset.stepBy));
                });
            });
        }
    });
})();
