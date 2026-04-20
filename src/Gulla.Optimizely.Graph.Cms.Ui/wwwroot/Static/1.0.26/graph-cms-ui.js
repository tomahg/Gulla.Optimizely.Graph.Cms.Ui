(function () {
    'use strict';

    var state = {
        site: null,
        lang: null,
        slot: 'one',
        pinned: null,
        pinnedNames: {},
        synonyms: null
    };

    function $(id) { return document.getElementById(id); }

    function api(path) { return '/GraphCmsUi/api' + path; }

    function qs(extra) {
        var params = new URLSearchParams();
        if (state.site) params.set('site', state.site);
        if (state.lang) params.set('lang', state.lang);
        if (extra) for (var k in extra) params.set(k, extra[k]);
        return params.toString();
    }

    function synQs() {
        var params = new URLSearchParams();
        if (state.site) params.set('site', state.site);
        if (state.lang) params.set('lang', state.lang);
        if (state.slot) params.set('slot', state.slot);
        return params.toString();
    }

    function escapeHtml(s) {
        return String(s == null ? '' : s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    // ------- Tabs -------

    function activateTab(name) {
        var tabs = document.querySelectorAll('.gulla-tab');
        tabs.forEach(function (t) {
            var active = t.dataset.tab === name;
            t.setAttribute('aria-selected', active ? 'true' : 'false');
        });
        document.querySelectorAll('.gulla-tab-panel').forEach(function (p) { p.hidden = true; });
        var panel = $('gulla-tab-' + name);
        if (panel) panel.hidden = false;

        // Refresh the tab's data every time it's activated. Cheap for our list sizes,
        // and guarantees the list matches what's currently in Graph.
        if (!state.site || !state.lang) return;
        if (name === 'best-bets') loadPinned();
        else if (name === 'synonyms') loadSynonyms();
    }

    // ------- Pinned Results -------

    function renderPinned() {
        var container = $('gulla-pinned-list');
        if (!container) return;

        if (state.pinned === null) {
            container.innerHTML = '<div class="gulla-list__empty">Loading&hellip;</div>';
            return;
        }

        var filter = ($('gulla-pinned-filter').value || '').toLowerCase();
        var items = state.pinned.filter(function (p) {
            return !filter || (p.phrases || '').toLowerCase().indexOf(filter) >= 0;
        });

        if (!items.length) {
            container.innerHTML = '<div class="gulla-list__empty">There are no pinned results yet</div>';
            return;
        }

        container.innerHTML = items.map(function (item) {
            var chips = (item.phrases || '').split(',').map(function (p) {
                return '<span class="gulla-chip">' + escapeHtml(p.trim()) + '</span>';
            }).join('');
            var resolved = state.pinnedNames[item.targetKey];
            var title = resolved ? resolved.name : item.targetKey;
            var urlLine = resolved && resolved.url ? '<div class="gulla-picker__result-url">' + escapeHtml(resolved.url) + '</div>' : '';
            return '<div class="gulla-list__row">' +
                '<button class="gulla-button gulla-list__row-actions" data-delete-pinned="' + escapeHtml(item.id) + '">Delete</button>' +
                '<div class="gulla-list__row-title">' + escapeHtml(title) + '</div>' +
                urlLine +
                '<div class="gulla-list__row-body">Language: ' + escapeHtml(item.language || '') + ' &middot; Priority: ' + escapeHtml(item.priority) + '</div>' +
                '<div class="gulla-list__chips">' + chips + '</div>' +
                '</div>';
        }).join('');
    }

    function loadPinned() {
        return fetch(api('/pinned?' + qs()))
            .then(function (r) { return r.ok ? r.json() : []; })
            .then(function (items) {
                state.pinned = items || [];
                renderPinned();
                return resolvePinnedNames();
            });
    }

    function resolvePinnedNames() {
        var guids = state.pinned.map(function (p) { return p.targetKey; }).filter(Boolean);
        var unique = guids.filter(function (g, i) { return guids.indexOf(g) === i; });
        var missing = unique.filter(function (g) { return !state.pinnedNames[g]; });
        if (!missing.length) { renderPinned(); return; }

        Promise.all(missing.map(function (g) {
            return fetch(api('/pinned/resolve-content') + '?guid=' + encodeURIComponent(g))
                .then(function (r) { return r.ok ? r.json() : null; })
                .catch(function () { return null; });
        })).then(function (results) {
            results.forEach(function (r) {
                if (r && r.contentGuid) state.pinnedNames[r.contentGuid] = r;
            });
            renderPinned();
        });
    }

    function bindPinned() {
        var form = $('gulla-pinned-form');
        if (!form) return;

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var body = {
                phrases: $('gulla-pinned-phrases').value.trim(),
                targetKey: $('gulla-pinned-target').value.trim(),
                language: state.lang,
                priority: parseInt($('gulla-pinned-priority').value, 10) || 1
            };
            if (!body.phrases || !body.targetKey) {
                alert('Please pick a target content item.');
                return;
            }

            fetch(api('/pinned?' + qs()), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            }).then(function (r) {
                if (!r.ok) return alert('Failed to add pinned result.');
                form.reset();
                clearPickerSelection();
                loadPinned();
            });
        });

        $('gulla-pinned-list').addEventListener('click', function (e) {
            var btn = e.target.closest('[data-delete-pinned]');
            if (!btn) return;
            var id = btn.getAttribute('data-delete-pinned');
            if (!confirm('Delete this pinned result?')) return;
            fetch(api('/pinned/' + encodeURIComponent(id) + '?' + qs()), { method: 'DELETE' })
                .then(function () { loadPinned(); });
        });

        $('gulla-pinned-filter').addEventListener('input', renderPinned);

        bindContentPicker();
    }

    // ------- Content picker (Optimizely Dojo dialog) -------

    function bindContentPicker() {
        var pickBtn = $('gulla-pinned-target-pick');
        if (!pickBtn) return;
        pickBtn.addEventListener('click', openOptimizelyContentDialog);
    }

    function openOptimizelyContentDialog() {
        if (typeof window.require !== 'function') {
            console.error('[Gulla.Optimizely.Graph.Cms.Ui] window.require is not defined. ' +
                'Check that Gulla.Optimizely.Graph.Cms.Ui is registered as a module in ' +
                'modules/_protected/Gulla.Optimizely.Graph.Cms.Ui/module.config and that the ' +
                'site was restarted. Scripts currently on the page: ',
                Array.from(document.scripts).map(function (s) { return s.src; }).filter(Boolean));
            alert('The Optimizely shell is not loaded. Open this page from the CMS menu.\n\n' +
                  'See the browser console for diagnostics.');
            return;
        }

        window.require([
            'dojo/aspect',
            'epi-cms/widget/ContentSelectorDialog',
            'epi/shell/widget/dialog/Dialog'
        ], function (aspect, ContentSelectorDialog, Dialog) {
            var selector = new ContentSelectorDialog({
                roots: ['1'],          // "1" is the content-tree root
                showButtons: false,    // Outer Dialog supplies OK/Cancel
                showSearchBox: true,
                searchArea: 'cms/pages'
            });

            var dialog = new Dialog({
                title: 'Select content',
                dialogClass: 'epi-dialog-contentSelector',
                content: selector
            });

            aspect.after(dialog, 'onExecute', function () {
                var value = selector.get('value');
                if (value) applyPickedContent(value);
            }, true);

            aspect.after(dialog, 'onHide', function () {
                setTimeout(function () {
                    try { selector.destroyRecursive(); } catch (e) { /* noop */ }
                    try { dialog.destroyRecursive(); } catch (e) { /* noop */ }
                }, 0);
            }, true);

            dialog.show();
        });
    }

    function applyPickedContent(contentRef) {
        fetch(api('/pinned/resolve-content') + '?contentLink=' + encodeURIComponent(contentRef), { credentials: 'same-origin' })
            .then(function (r) { return r.ok ? r.json() : null; })
            .then(function (resolved) {
                if (!resolved || !resolved.contentGuid) {
                    alert('Could not resolve the selected content to a GUID.');
                    clearPickerSelection();
                    return;
                }
                $('gulla-pinned-target').value = resolved.contentGuid;
                $('gulla-pinned-target-display').value = resolved.name;
                var selected = $('gulla-pinned-target-selected');
                if (resolved.url) {
                    selected.textContent = resolved.url;
                    selected.hidden = false;
                } else {
                    selected.hidden = true;
                    selected.textContent = '';
                }
            });
    }

    function clearPickerSelection() {
        $('gulla-pinned-target').value = '';
        var display = $('gulla-pinned-target-display');
        if (display) display.value = '';
        var selected = $('gulla-pinned-target-selected');
        if (selected) { selected.hidden = true; selected.textContent = ''; }
    }

    // ------- Synonyms -------

    function directionIcon(bidi) {
        return '<span class="gulla-direction-icon">' + (bidi ? '&lt;&gt;' : '&gt;') + '</span>';
    }

    function renderSynonyms() {
        var tbody = document.querySelector('#gulla-syn-list tbody');
        if (!tbody) return;

        if (state.synonyms === null) {
            tbody.innerHTML = '<tr class="gulla-table__empty"><td colspan="4">Loading&hellip;</td></tr>';
            return;
        }

        var filter = ($('gulla-syn-filter').value || '').toLowerCase();
        var rows = state.synonyms.filter(function (s) {
            if (!filter) return true;
            var blob = (s.phrases || []).join(',') + ' ' + (s.synonym || '');
            return blob.toLowerCase().indexOf(filter) >= 0;
        });

        if (!rows.length) {
            tbody.innerHTML = '<tr class="gulla-table__empty"><td colspan="4">No synonyms yet</td></tr>';
            return;
        }

        tbody.innerHTML = rows.map(function (s) {
            return '<tr>' +
                '<td>' + escapeHtml((s.phrases || []).join(', ')) + '</td>' +
                '<td class="gulla-table__direction">' + directionIcon(s.bidirectional) + '</td>' +
                '<td>' + escapeHtml(s.synonym || '') + '</td>' +
                '<td><button class="gulla-button" data-delete-syn="' + escapeHtml(s.rowKey) + '">Delete</button></td>' +
                '</tr>';
        }).join('');
    }

    function loadSynonyms() {
        return fetch(api('/synonyms?' + synQs()))
            .then(function (r) { return r.ok ? r.json() : []; })
            .then(function (items) { state.synonyms = items || []; renderSynonyms(); });
    }

    function bindSynonyms() {
        var form = $('gulla-syn-form');
        if (!form) return;

        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var body = {
                phrases: $('gulla-syn-phrases').value.trim(),
                synonym: $('gulla-syn-synonym').value.trim(),
                bidirectional: $('gulla-syn-bidi').checked
            };
            if (!body.phrases || !body.synonym) return;

            fetch(api('/synonyms?' + synQs()), {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(body)
            }).then(function (r) {
                if (!r.ok) return alert('Failed to add synonym.');
                form.reset();
                loadSynonyms();
            });
        });

        document.querySelector('#gulla-syn-list').addEventListener('click', function (e) {
            var btn = e.target.closest('[data-delete-syn]');
            if (!btn) return;
            var key = btn.getAttribute('data-delete-syn');
            if (!confirm('Delete this synonym?')) return;
            fetch(api('/synonyms/' + encodeURIComponent(key) + '?' + synQs()), { method: 'DELETE' })
                .then(function () { loadSynonyms(); });
        });

        $('gulla-syn-filter').addEventListener('input', renderSynonyms);

        $('gulla-syn-import-file').addEventListener('change', function (e) {
            var file = e.target.files && e.target.files[0];
            if (!file) return;
            var fd = new FormData();
            fd.append('file', file);
            fetch(api('/synonyms/import?' + synQs()), { method: 'POST', body: fd })
                .then(function (r) { return r.ok ? r.json() : Promise.reject(); })
                .then(function (res) {
                    alert('Imported ' + res.imported + ' synonyms.');
                    loadSynonyms();
                })
                .catch(function () { alert('Import failed.'); });
            e.target.value = '';
        });

        $('gulla-syn-export').addEventListener('click', function (e) {
            e.preventDefault();
            window.location.href = api('/synonyms/export?' + synQs());
        });
    }

    // ------- Boot -------

    function refreshAll() {
        loadPinned();
        loadSynonyms();
    }

    function init() {
        var siteSelect = $('gulla-site-select');
        var langSelect = $('gulla-lang-select');
        var slotSelect = $('gulla-syn-slot');
        state.site = siteSelect ? siteSelect.value : null;
        state.lang = langSelect ? langSelect.value : null;
        state.slot = (slotSelect && slotSelect.value) || 'one';

        if (siteSelect) siteSelect.addEventListener('change', function () { state.site = siteSelect.value; refreshAll(); });
        if (langSelect) langSelect.addEventListener('change', function () { state.lang = langSelect.value; refreshAll(); });
        if (slotSelect) slotSelect.addEventListener('change', function () { state.slot = slotSelect.value; loadSynonyms(); });

        document.querySelectorAll('.gulla-tab').forEach(function (tab) {
            tab.addEventListener('click', function () { activateTab(tab.dataset.tab); });
        });
        activateTab((window.gullaGraphUi && window.gullaGraphUi.initialTab) || 'best-bets');

        bindPinned();
        bindSynonyms();
        if (state.site && state.lang) {
            refreshAll();
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
