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
})();
