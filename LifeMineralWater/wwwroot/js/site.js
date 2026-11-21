(function () {
    'use strict';

    // Auto-dismiss toast notifications.
    var toasts = document.querySelectorAll('.toast-msg');
    if (toasts.length) {
        setTimeout(function () {
            toasts.forEach(function (t) { t.style.opacity = '0'; t.style.transition = 'opacity .35s'; });
            setTimeout(function () { toasts.forEach(function (t) { t.remove(); }); }, 400);
        }, 5000);
    }

    // Confirmation modal for destructive / sensitive actions.
    var modal = null;
    function ensureModal() {
        if (modal) return modal;
        var wrapper = document.createElement('div');
        wrapper.innerHTML =
            '<div class="modal fade" tabindex="-1" id="lmConfirmModal">' +
            '  <div class="modal-dialog modal-dialog-centered"><div class="modal-content">' +
            '    <div class="modal-header"><h5 class="modal-title">Please confirm</h5>' +
            '      <button type="button" class="btn-close" data-bs-dismiss="modal"></button></div>' +
            '    <div class="modal-body"><p class="mb-0" id="lmConfirmText"></p></div>' +
            '    <div class="modal-footer"><button type="button" class="btn btn-light" data-bs-dismiss="modal">Cancel</button>' +
            '      <button type="button" class="btn btn-danger" id="lmConfirmYes">Confirm</button></div>' +
            '  </div></div></div>';
        document.body.appendChild(wrapper.firstElementChild);
        modal = new bootstrap.Modal(document.getElementById('lmConfirmModal'));
        return modal;
    }

    document.addEventListener('click', function (e) {
        var trigger = e.target.closest('[data-confirm]');
        if (!trigger) return;
        e.preventDefault();
        var text = trigger.getAttribute('data-confirm') || 'Are you sure?';
        var m = ensureModal();
        document.getElementById('lmConfirmText').textContent = text;
        var yes = document.getElementById('lmConfirmYes');
        var handler = function () {
            yes.removeEventListener('click', handler);
            m.hide();
            if (trigger.form) trigger.form.submit();
            else window.location.href = trigger.href;
        };
        yes.addEventListener('click', handler);
        m.show();
    });

    // Quick filter: Enter key submits forms marked with data-auto-submit.
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter') return;
        var input = e.target.closest('[data-auto-submit] input, [data-auto-submit] select');
        if (input && input.form) { e.preventDefault(); input.form.submit(); }
    });
})();
