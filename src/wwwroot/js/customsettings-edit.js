/**
 * Custom Settings Editor
 * Handles form rendering and submission - validation done on server
 */

// Global variables
const groupId = window.CUSTOMSETTINGS_GROUP_ID;
const initialSiteId = window.CUSTOMSETTINGS_CURRENT_SITE_ID || null;
let schema = null;
let currentSiteId = null;
let currentLanguage = null;
let currentData = {};
let fallbackInfo = {};
let masterLanguage = null;
let hasUnsavedChanges = false;
let originalFormData = {};
let isFormInitializing = true;

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', async () => {
    try {
        await loadContext();
        await loadSchema();
        await loadSettings();
        renderForm();
        document.getElementById('loadingIndicator').style.display = 'none';
        document.getElementById('settingsForm').style.display = 'block';
    } catch (error) {
        console.error('Initialization error:', error);
        showError('Failed to load settings: ' + error.message);
        document.getElementById('loadingIndicator').style.display = 'none';
    }
    setTimeout(() => {
        isFormInitializing = false;
        console.log('Form initialization complete');
    }, 300);
});

// Load sites and languages
async function loadContext() {
    const response = await fetch('/customsettings/api/context');
    if (!response.ok) throw new Error('Failed to load context');

    const context = await response.json();

    const siteSelector = document.getElementById('siteSelector');
    const languageSelector = document.getElementById('languageSelector');

    // Populate sites only
    context.sites.forEach(site => {
        const option = document.createElement('option');
        option.value = site.id;
        option.textContent = site.name;
        siteSelector.appendChild(option);
    });

    // Set defaults
    if (initialSiteId && siteSelector.querySelector(`option[value="${initialSiteId}"]`)) {
        siteSelector.value = initialSiteId;
        currentSiteId = initialSiteId;
        console.log('Using current CMS site:', currentSiteId);
    } else {
        currentSiteId = siteSelector.value;
        console.log('Using default site from list:', currentSiteId);
    }

    // Load languages for selected site
    await loadLanguagesForSite(currentSiteId);
    currentLanguage = languageSelector.value;

    // Listen for site changes
    siteSelector.addEventListener('change', async () => {
        if (hasUnsavedChanges && !confirm('You have unsaved changes. Do you want to discard them?')) {
            siteSelector.value = currentSiteId;
            return;
        }

        currentSiteId = siteSelector.value;
        console.log('Site changed to:', currentSiteId);

        isFormInitializing = true;
        await loadLanguagesForSite(currentSiteId);
        await loadSettings();
        renderForm();
        setTimeout(() => { isFormInitializing = false; }, 300);
    });

    // Listen for language changes
    languageSelector.addEventListener('change', async () => {
        if (hasUnsavedChanges && !confirm('You have unsaved changes. Do you want to discard them?')) {
            languageSelector.value = currentLanguage;
            return;
        }

        currentLanguage = languageSelector.value;
        console.log('Language changed to:', currentLanguage);

        isFormInitializing = true;
        await loadSettings();
        renderForm();
        setTimeout(() => { isFormInitializing = false; }, 300);
    });
}

// Load languages for a specific site
async function loadLanguagesForSite(siteId) {
    try {
        const response = await fetch(`/customsettings/api/context/languages/${siteId}`);
        if (!response.ok) {
            console.error('Failed to load languages for site');
            return;
        }

        const data = await response.json();
        const languageSelector = document.getElementById('languageSelector');

        // Save current selection if exists
        const previousLanguage = currentLanguage;

        // Clear and repopulate
        languageSelector.innerHTML = '';

        data.languages.forEach(lang => {
            const option = document.createElement('option');
            option.value = lang.code;
            option.textContent = lang.name;
            languageSelector.appendChild(option);
        });

        // Try to keep previous language if available, otherwise select first
        if (previousLanguage && languageSelector.querySelector(`option[value="${previousLanguage}"]`)) {
            languageSelector.value = previousLanguage;
            currentLanguage = previousLanguage;
            console.log('Language kept:', currentLanguage);
        } else {
            currentLanguage = languageSelector.value;
            console.log('Language changed to:', currentLanguage);
        }
    } catch (error) {
        console.error('Error loading languages for site:', error);
    }
}

// Load JSON Schema
async function loadSchema() {
    const response = await fetch(`/customsettings/api/schema/${groupId}`);
    if (!response.ok) throw new Error('Failed to load schema');
    schema = await response.json();
}

// Load settings data
async function loadSettings() {
    const params = new URLSearchParams();
    if (currentSiteId) params.append('siteId', currentSiteId);
    if (currentLanguage) params.append('language', currentLanguage);

    console.log('Loading settings for:', { siteId: currentSiteId, language: currentLanguage });

    const response = await fetch(`/customsettings/api/settings/${groupId}?${params}`);
    if (!response.ok) throw new Error('Failed to load settings');

    const result = await response.json();

    currentData = result.values || result;
    fallbackInfo = result.fallbackInfo || {};
    masterLanguage = result.masterLanguage || 'en';

    console.log('Loaded settings:', currentData);
    console.log('Fallback info:', fallbackInfo);
}

// Render form based on schema
function renderForm() {
    const formFields = document.getElementById('formFields');
    formFields.innerHTML = '';

    if (!schema || !schema.properties) return;

    for (const [fieldName, fieldSchema] of Object.entries(schema.properties)) {
        const fieldGroup = createFieldGroup(fieldName, fieldSchema, currentData[fieldName]);
        formFields.appendChild(fieldGroup);
    }

    attachChangeListeners();
    setTimeout(() => markFormAsClean(), 100);
}

// Create field group
function createFieldGroup(name, fieldSchema, value) {
    const group = document.createElement('div');
    group.className = 'form-group';
    group.id = `field-${name}`;

    const labelContainer = document.createElement('div');
    labelContainer.className = 'label-container';

    const label = document.createElement('label');
    label.htmlFor = `input-${name}`;
    label.textContent = fieldSchema.title || name;

    if (schema.required && schema.required.includes(name)) {
        label.innerHTML += ' <span style="color: #d32f2f;">*</span>';
    }

    labelContainer.appendChild(label);

    if (fieldSchema.hasFallback) {
        const fallbackIcon = document.createElement('span');
        fallbackIcon.className = 'fallback-icon';
        fallbackIcon.innerHTML = '<i class="fas fa-language"></i>';
        fallbackIcon.title = 'This field supports fallback to master language';
        labelContainer.appendChild(fallbackIcon);
    }

    group.appendChild(labelContainer);

    const input = createInput(name, fieldSchema, value);
    group.appendChild(input);

    if (fallbackInfo[name] && fallbackInfo[name].isFallback) {
        const inheritanceNotice = document.createElement('div');
        inheritanceNotice.className = 'inheritance-notice';
        const noticeIcon = document.createElement('i');
        noticeIcon.className = 'fas fa-info-circle';
        inheritanceNotice.appendChild(noticeIcon);
        const noticeSpan = document.createElement('span');
        noticeSpan.appendChild(document.createTextNode('Inherited from '));
        const noticeLang = document.createElement('strong');
        noticeLang.textContent = fallbackInfo[name].masterLanguage.toUpperCase();
        noticeSpan.appendChild(noticeLang);
        noticeSpan.appendChild(document.createTextNode(': "' + fallbackInfo[name].masterValue + '"'));
        inheritanceNotice.appendChild(noticeSpan);
        group.appendChild(inheritanceNotice);
    }

    if (fieldSchema.description) {
        const desc = document.createElement('div');
        desc.className = 'field-description';
        desc.textContent = fieldSchema.description;
        group.appendChild(desc);
    }

    // Error container for server-side validation errors
    const error = document.createElement('div');
    error.className = 'validation-error';
    error.id = `error-${name}`;
    group.appendChild(error);

    return group;
}

function createInput(name, fieldSchema, value) {
    const type = fieldSchema.type;
    let input;

    if (type === 'boolean') {
        const container = document.createElement('div');
        container.className = 'checkbox-group';

        input = document.createElement('input');
        input.type = 'checkbox';
        input.id = `input-${name}`;
        input.name = name;
        input.checked = value === true;

        const label = document.createElement('label');
        label.htmlFor = `input-${name}`;
        label.textContent = 'Enable';

        container.appendChild(input);
        container.appendChild(label);

        return container;
    }

    if (type === 'integer' || type === 'number') {
        input = document.createElement('input');
        input.type = 'number';
        input.step = type === 'number' ? 'any' : '1';
        if (fieldSchema.minimum !== undefined) input.min = fieldSchema.minimum;
        if (fieldSchema.maximum !== undefined) input.max = fieldSchema.maximum;
        input.value = value ?? fieldSchema.default ?? '';

        if (fieldSchema.hasFallback && fallbackInfo[name] && !value) {
            input.placeholder = `${fallbackInfo[name].masterValue} (from ${fallbackInfo[name].masterLanguage.toUpperCase()})`;
            input.classList.add('has-fallback-value');
        }
    }
    else if (fieldSchema.enum) {
        input = document.createElement('select');
        fieldSchema.enum.forEach(opt => {
            const option = document.createElement('option');
            option.value = opt;
            option.textContent = opt;
            if (opt === value) option.selected = true;
            input.appendChild(option);
        });
    }
    else if (type === 'string' && fieldSchema.format === 'date-time') {
        return createDateTimePicker(name, value);
    }
    else if (type === 'string' && fieldSchema.format === 'url-picker') {
        return createUrlPickerInput(name, fieldSchema, value);
    }
    else if (fieldSchema.format === 'page-reference') {
        return createPageReferenceInput(name, fieldSchema, value);
    }
    else if (type === 'string' && fieldSchema.format === 'uri') {
        input = document.createElement('input');
        input.type = 'url';
        if (fieldSchema.maxLength) input.maxLength = fieldSchema.maxLength;
        if (fieldSchema.placeholder) input.placeholder = fieldSchema.placeholder;
        else input.placeholder = 'https://';
        input.value = value ?? fieldSchema.default ?? '';

        if (fieldSchema.hasFallback && fallbackInfo[name] && !input.value) {
            input.placeholder = `${fallbackInfo[name].masterValue} (from ${fallbackInfo[name].masterLanguage.toUpperCase()})`;
            input.classList.add('has-fallback-value');
        }
    }
    else if (type === 'string' && fieldSchema.format === 'uuid') {
        input = document.createElement('input');
        input.type = 'text';
        input.placeholder = 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx';
        input.pattern = '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}';
        input.value = value ?? fieldSchema.default ?? '';

        if (fieldSchema.hasFallback && fallbackInfo[name] && !input.value) {
            input.placeholder = `${fallbackInfo[name].masterValue} (from ${fallbackInfo[name].masterLanguage.toUpperCase()})`;
            input.classList.add('has-fallback-value');
        }
    }
    else if (type === 'array') {
        return createArrayInput(name, fieldSchema, value);
    }
    else {
        input = document.createElement('input');
        input.type = 'text';
        if (fieldSchema.maxLength) input.maxLength = fieldSchema.maxLength;
        if (fieldSchema.placeholder) input.placeholder = fieldSchema.placeholder;
        input.value = value ?? fieldSchema.default ?? '';

        if (fieldSchema.hasFallback && fallbackInfo[name] && !input.value) {
            input.placeholder = `${fallbackInfo[name].masterValue} (from ${fallbackInfo[name].masterLanguage.toUpperCase()})`;
            input.classList.add('has-fallback-value');
        }
    }

    input.id = `input-${name}`;
    input.name = name;

    // NO VALIDATION LISTENER - blur removed completely

    return input;
}

// Create array input
function createArrayInput(name, fieldSchema, value) {
    const container = document.createElement('div');
    container.className = 'array-field';
    container.id = `array-${name}`;

    const itemsContainer = document.createElement('div');
    itemsContainer.id = `array-items-${name}`;
    container.appendChild(itemsContainer);

    const addButton = document.createElement('button');
    addButton.type = 'button';
    addButton.className = 'btn btn-secondary array-add';
    addButton.innerHTML = '<i class="fas fa-plus"></i> Add Item';
    addButton.onclick = () => addArrayItem(name, fieldSchema.items, itemsContainer);
    container.appendChild(addButton);

    if (Array.isArray(value)) {
        value.forEach(item => addArrayItem(name, fieldSchema.items, itemsContainer, item));
    }

    return container;
}

// Add array item
function addArrayItem(arrayName, itemSchema, container, value) {
    const item = document.createElement('div');
    item.className = 'array-item';

    const input = createInput(`${arrayName}[]`, itemSchema, value);
    item.appendChild(input);

    const removeBtn = document.createElement('button');
    removeBtn.className = 'array-item-remove';
    removeBtn.innerHTML = '<i class="fas fa-times"></i>';
    removeBtn.onclick = () => {
        item.remove();
        markFormAsDirty();
    };
    item.appendChild(removeBtn);

    container.appendChild(item);
}

let changeCheckTimeout;
function handleInputChange(event) {
    if (isFormInitializing) {
        console.log('Change ignored - initializing');
        return;
    }
    clearTimeout(changeCheckTimeout);
    changeCheckTimeout = setTimeout(() => checkForChanges(), 150);
}

function checkForChanges() {
    if (isFormInitializing) return;
    const currentFormData = collectFormData();
    const hasChanges = !areDataEqual(currentFormData, originalFormData);

    if (hasChanges !== hasUnsavedChanges) {
        hasUnsavedChanges = hasChanges;
        updateUnsavedChangesIndicator();
        console.log(hasChanges ? 'Unsaved changes detected' : 'Form matches saved state');
    }
}

function areDataEqual(data1, data2) {
    if (data1 === data2) return true;
    if (data1 == null || data2 == null) return false;

    const keys1 = Object.keys(data1);
    const keys2 = Object.keys(data2);
    if (keys1.length !== keys2.length) return false;

    for (const key of keys1) {
        if (!keys2.includes(key)) return false;

        const val1 = normalizeValue(data1[key]);
        const val2 = normalizeValue(data2[key]);

        if (Array.isArray(val1) && Array.isArray(val2)) {
            if (!areArraysEqual(val1, val2)) return false;
            continue;
        }

        if (typeof val1 === 'object' && typeof val2 === 'object' && val1 !== null && val2 !== null) {
            if (!areDataEqual(val1, val2)) return false;
            continue;
        }

        if (val1 !== val2) return false;
    }
    return true;
}

function normalizeValue(value) {
    if (value === '' || value === null || value === undefined) return null;
    return value;
}

function areArraysEqual(arr1, arr2) {
    if (arr1.length !== arr2.length) return false;
    for (let i = 0; i < arr1.length; i++) {
        if (normalizeValue(arr1[i]) !== normalizeValue(arr2[i])) return false;
    }
    return true;
}

function attachChangeListeners() {
    const inputs = document.querySelectorAll('#formFields input, #formFields select, #formFields textarea');
    console.log(`Attaching listeners to ${inputs.length} inputs`);

    inputs.forEach((input, index) => {
        const eventType = (input.type === 'checkbox' || input.tagName === 'SELECT') ? 'change' : 'input';
        console.log(`   [${index}] ${input.id || input.name} (${input.type}) → ${eventType} event`);

        input.addEventListener(eventType, handleInputChange);
    });
}

function markFormAsClean() {
    hasUnsavedChanges = false;
    originalFormData = collectFormData();
    updateUnsavedChangesIndicator();
    console.log('Form marked as clean. Baseline:', originalFormData);
}

function markFormAsDirty() {
    if (!isFormInitializing) {
        checkForChanges();
    }
}

function updateUnsavedChangesIndicator() {
    const saveBtn = document.querySelector('.btn-primary');
    const indicator = document.getElementById('unsavedChangesIndicator');

    if (hasUnsavedChanges) {
        saveBtn?.classList.add('has-changes');
        if (indicator) indicator.style.display = 'flex';
    } else {
        saveBtn?.classList.remove('has-changes');
        if (indicator) indicator.style.display = 'none';
    }
}

document.getElementById('settingsForm')?.addEventListener('submit', async (e) => {
    e.preventDefault();

    const formData = collectFormData();

    const params = new URLSearchParams();
    if (currentSiteId) params.append('siteId', currentSiteId);
    if (currentLanguage) params.append('language', currentLanguage);

    const startTime = performance.now();
    const saveBtn = document.querySelector('.btn-primary');
    const originalBtnContent = saveBtn.innerHTML;
    saveBtn.disabled = true;
    saveBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Saving...';

    // Clear any previous validation errors
    clearValidationErrors();

    try {
        const response = await fetch(`/customsettings/api/settings/${groupId}?${params}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(formData)
        });

        const endTime = performance.now();
        const duration = Math.round(endTime - startTime);

        // Handle server-side validation errors
        if (!response.ok) {
            if (response.status === 400) {
                // Validation errors from server
                const errorData = await response.json();
                displayServerValidationErrors(errorData.errors);
                showError('Please fix validation errors before saving');
            } else {
                throw new Error('Failed to save settings');
            }
            return;
        }

        console.log(`✅ Settings saved in ${duration}ms`);
        showSuccess(duration);
        markFormAsClean();

        if (duration > 1000) {
            console.warn(`⚠️ Save operation took ${duration}ms (NFR-3 requirement: <1000ms)`);
        }
    } catch (error) {
        console.error('Save error:', error);
        showError('Failed to save settings: ' + error.message);
    } finally {
        saveBtn.disabled = false;
        saveBtn.innerHTML = originalBtnContent;
    }
});

function displayServerValidationErrors(errors) {
    if (!errors) return;

    for (const [fieldName, messages] of Object.entries(errors)) {
        const fieldGroup = document.getElementById(`field-${fieldName}`);
        const errorDiv = document.getElementById(`error-${fieldName}`);

        if (fieldGroup && errorDiv) {
            fieldGroup.classList.add('field-error');
            errorDiv.textContent = messages[0]; // Display first error message
        }
    }

    // Scroll to first error
    const firstError = document.querySelector('.field-error');
    if (firstError) {
        firstError.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}

function clearValidationErrors() {
    document.querySelectorAll('.field-error').forEach(el => el.classList.remove('field-error'));
    document.querySelectorAll('.validation-error').forEach(el => el.textContent = '');
}

function collectFormData() {
    const data = {};

    for (const [fieldName, fieldSchema] of Object.entries(schema.properties)) {
        if (fieldSchema.type === 'array') {
            const items = [];
            const arrayInputs = document.querySelectorAll(`#array-items-${fieldName} input, #array-items-${fieldName} select`);
            arrayInputs.forEach(inp => {
                if (inp.value) items.push(inp.value);
            });
            data[fieldName] = items;
            continue;
        }

        const input = document.getElementById(`input-${fieldName}`);

        if (!input) continue;

        if (fieldSchema.type === 'boolean') {
            data[fieldName] = input.checked;
        }
        else if (fieldSchema.type === 'integer') {
            data[fieldName] = input.value ? parseInt(input.value, 10) : null;
        }
        else if (fieldSchema.type === 'number') {
            data[fieldName] = input.value ? parseFloat(input.value) : null;
        }
        else if (fieldSchema.type === 'string' && fieldSchema.format === 'uuid') {
            const val = input.value?.trim();
            const uuidRegex = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;
            data[fieldName] = (val && uuidRegex.test(val)) ? val : null;
        }
        else if (fieldSchema.type === 'string' && fieldSchema.format === 'date-time') {
            // hidden input stores ISO string set by picker
            data[fieldName] = input.value || null;
        }
        else if (fieldSchema.type === 'string' && fieldSchema.format === 'url-picker') {
            // href input inside the url-picker container
            data[fieldName] = input.value || null;
        }
        else if (fieldSchema.format === 'page-reference') {
            // hidden input stores JSON string: {"id":5,"workId":0,"providerName":null}
            const raw = input.value;
            data[fieldName] = raw ? JSON.parse(raw) : null;
        }
        else {
            data[fieldName] = input.value || null;
        }
    }

    return data;
}

document.getElementById('resetBtn')?.addEventListener('click', async () => {
    if (confirm('Are you sure you want to reset all fields to their default values?')) {
        isFormInitializing = true;
        currentData = {};
        renderForm();
        setTimeout(() => { isFormInitializing = false; }, 300);
    }
});

function showSuccess(duration) {
    const alert = document.getElementById('successAlert');
    alert.innerHTML = '<i class="fas fa-check-circle"></i> Settings saved successfully!';
    alert.style.display = 'flex';
    setTimeout(() => alert.style.display = 'none', 3000);
}

function showError(message) {
    const alert = document.getElementById('errorAlert');
    document.getElementById('errorMessage').textContent = message;
    alert.style.display = 'flex';
    setTimeout(() => alert.style.display = 'none', 5000);
}

window.addEventListener('beforeunload', (e) => {
    if (hasUnsavedChanges) {
        e.preventDefault();
        e.returnValue = '';
    }
});

// Registry of active Dijit widgets keyed by field name, used for value collection.
const _dijitWidgets = {};

function createDateTimePicker(name, initialValue) {
    const wrapper = document.createElement('div');
    wrapper.className = 'epi-dijit-datetime-wrapper';
    wrapper.style.display = 'flex';
    wrapper.style.alignItems = 'center';
    wrapper.style.gap = '6px';

    // Placeholder divs for Dijit widgets
    const dateNode = document.createElement('div');
    dateNode.id = `dijit-date-${name}`;

    const timeNode = document.createElement('div');
    timeNode.id = `dijit-time-${name}`;

    // Hidden input stores the combined ISO value for collectFormData
    const hidden = document.createElement('input');
    hidden.type = 'hidden';
    hidden.id = `input-${name}`;
    hidden.name = name;

    wrapper.appendChild(dateNode);
    wrapper.appendChild(timeNode);
    wrapper.appendChild(hidden);

    // Initialise Dijit widgets after the wrapper is in the DOM
    requestAnimationFrame(() => {
        if (typeof require === 'undefined') {
            // Dojo not available - fall back to native input
            _dijitFallback(wrapper, hidden, name, initialValue);
            return;
        }

        require(['dijit/form/DateTextBox', 'dijit/form/TimeTextBox'], function (DateTextBox, TimeTextBox) {
            const initDate = initialValue ? new Date(initialValue) : null;

            const dateWidget = new DateTextBox({
                name: `${name}_date`,
                value: initDate,
                constraints: { datePattern: 'yyyy-MM-dd' },
                style: 'width:130px',
                onChange: function () { _syncHidden(name, hidden); }
            }, dateNode);

            const timeWidget = new TimeTextBox({
                name: `${name}_time`,
                value: initDate,
                constraints: { timePattern: 'HH:mm', clickableIncrement: 'T00:30:00', visibleIncrement: 'T00:30:00', visibleRange: 'T04:00:00' },
                style: 'width:90px',
                onChange: function () { _syncHidden(name, hidden); }
            }, timeNode);

            dateWidget.startup();
            timeWidget.startup();

            _dijitWidgets[name] = { dateWidget, timeWidget };

            if (initDate) {
                hidden.value = initDate.toISOString();
            }
        });
    });

    return wrapper;
}

function _syncHidden(name, hidden) {
    const widgets = _dijitWidgets[name];
    if (!widgets) return;

    const dv = widgets.dateWidget.get('value');
    const tv = widgets.timeWidget.get('value');

    if (!dv) { hidden.value = ''; markFormAsDirty(); return; }

    const d = new Date(dv);
    if (tv) {
        d.setHours(tv.getHours(), tv.getMinutes(), 0, 0);
    } else {
        d.setHours(0, 0, 0, 0);
    }
    hidden.value = d.toISOString();
    markFormAsDirty();
}

function _dijitFallback(wrapper, hidden, name, initialValue) {
    // Native fallback when Dojo is unavailable
    const input = document.createElement('input');
    input.type = 'datetime-local';
    input.id = `input-${name}`;
    input.name = name;
    if (initialValue) {
        const d = new Date(initialValue);
        input.value = d.toISOString().slice(0, 16);
    }
    input.addEventListener('change', () => {
        hidden.value = input.value ? new Date(input.value).toISOString() : '';
        markFormAsDirty();
    });
    // Replace wrapper content with just the native input
    wrapper.innerHTML = '';
    wrapper.appendChild(input);
    hidden.id = `input-${name}`;
}

function _detectUrlType(href) {
    if (!href) return 'none';
    if (/^mailto:/i.test(href)) return 'email';
    if (/^https?:\/\//i.test(href)) return 'external';
    // Heuristic: common asset paths or file extensions → media
    if (/\.(jpg|jpeg|png|gif|webp|svg|pdf|doc|docx|xls|xlsx|ppt|pptx|mp4|mp3|zip|ico|bmp|tiff?)$/i.test(href)) return 'media';
    if (/^\/(globalassets|siteassets|contentassets)\//i.test(href)) return 'media';
    return 'internal';
}

// Returns the Font Awesome icon class for a given content type string
function _getContentIcon(contentType) {
    switch (contentType) {
        case 'media':  return 'far fa-image';
        case 'block':  return 'fas fa-cube';
        case 'folder': return 'far fa-folder';
        default:       return 'far fa-file-alt'; // page
    }
}

// ── Shared page tree helpers (used by URL picker dropdown + page-reference modal) ──

function _buildPageTree(pages) {
    const map = {};
    const roots = [];
    pages.forEach(p => { map[p.id] = { ...p, children: [] }; });
    pages.forEach(p => {
        if (p.parentId && map[p.parentId]) {
            map[p.parentId].children.push(map[p.id]);
        } else {
            roots.push(map[p.id]);
        }
    });
    function sortLevel(nodes) {
        nodes.sort((a, b) => a.name.localeCompare(b.name));
        nodes.forEach(n => sortLevel(n.children));
    }
    sortLevel(roots);
    return roots;
}

function createUrlPickerInput(name, fieldSchema, initialValue) {
    const container = document.createElement('div');
    container.className = 'url-picker';
    container.id = `input-${name}`;
    container.name = name;

    let currentValue = initialValue ?? '';

    // ── Display area (always visible, shows placeholder when empty) ──
    const display = document.createElement('div');
    display.className = 'epi-picker-display';
    container.appendChild(display);

    // ── Action row: button always visible, text changes with state ──
    const actionRow = document.createElement('div');
    actionRow.className = 'epi-picker-actions';
    const addBtn = document.createElement('button');
    addBtn.type = 'button';
    addBtn.className = 'epi-picker-btn';
    addBtn.textContent = 'Add Link';
    actionRow.appendChild(addBtn);
    container.appendChild(actionRow);

    function renderDisplay(val) {
        display.innerHTML = '';
        if (val && val !== '') {
            const type = _detectUrlType(val);
            let label = val;
            if (type === 'email') label = val.replace(/^mailto:/i, '');
            const iconClass = _getContentIcon(type === 'external' ? 'page' : type === 'media' ? 'media' : 'page');

            const iconSpan = document.createElement('span');
            iconSpan.className = 'epi-picker-display__icon';
            const iconEl = document.createElement('i');
            iconEl.className = iconClass;
            iconSpan.appendChild(iconEl);
            display.appendChild(iconSpan);

            const textSpan = document.createElement('span');
            textSpan.className = 'epi-picker-display__text';
            textSpan.textContent = label;
            display.appendChild(textSpan);

            const removeBtn = document.createElement('button');
            removeBtn.type = 'button';
            removeBtn.className = 'epi-picker-display__remove';
            removeBtn.title = 'Remove';
            const timesIcon = document.createElement('i');
            timesIcon.className = 'fas fa-times';
            removeBtn.appendChild(timesIcon);
            removeBtn.addEventListener('click', () => {
                currentValue = '';
                renderDisplay('');
                markFormAsDirty();
            });
            display.appendChild(removeBtn);
        } else {
            const placeholder = document.createElement('span');
            placeholder.className = 'epi-picker-display__placeholder';
            placeholder.textContent = 'No link selected';
            display.appendChild(placeholder);
        }
    }

    function openLinkModal(editValue) {
        document.getElementById('linkPickerModal')?.remove();

        const overlay = document.createElement('div');
        overlay.id = 'linkPickerModal';
        overlay.className = 'page-picker-overlay cs-picker-root';

        const modal = document.createElement('div');
        modal.className = 'page-picker-modal link-picker-modal';

        // Header
        const header = document.createElement('div');
        header.className = 'page-picker-header';
        header.innerHTML = '<span>' + (editValue ? 'Edit Link' : 'Add Link') + '</span>';
        const closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.className = 'page-picker-close';
        closeBtn.innerHTML = '&times;';
        closeBtn.addEventListener('click', () => overlay.remove());
        header.appendChild(closeBtn);
        modal.appendChild(header);

        // Body — three option cards with radio buttons
        const body = document.createElement('div');
        body.className = 'link-picker-body';

        const initialType = _detectUrlType(editValue ?? '');

        const optionDefs = [
            {
                type: 'external',
                label: 'External URL',
                inputType: 'url',
                placeholder: 'https://example.com',
                initValue: initialType === 'external' ? (editValue ?? '') : '',
            },
            {
                type: 'internal',
                label: 'Internal page',
                inputType: 'text',
                placeholder: '/en/about/',
                initValue: initialType === 'internal' ? (editValue ?? '') : '',
                readonly: true,
                hasBrowse: true,
                browseLabel: 'Select Page',
                browseTypeFilter: 'page',
            },
            {
                type: 'media',
                label: 'Media file',
                inputType: 'text',
                placeholder: '/globalassets/image.jpg',
                initValue: initialType === 'media' ? (editValue ?? '') : '',
                readonly: true,
                hasBrowse: true,
                browseLabel: 'Select Media',
                browseTypeFilter: 'media',
            },
            {
                type: 'email',
                label: 'Email address',
                inputType: 'email',
                placeholder: 'name@example.com',
                initValue: initialType === 'email' ? ((editValue ?? '').replace(/^mailto:/i, '')) : '',
            },
        ];

        const optionInputs = {};
        const optionRadios = {};
        const optionBrowse = {};
        const optionRows  = {};

        function syncSelected(selectedType) {
            optionDefs.forEach(def => {
                const isSelected = def.type === selectedType;
                optionInputs[def.type].disabled = !isSelected;
                if (optionBrowse[def.type]) optionBrowse[def.type].disabled = !isSelected;
                optionRows[def.type].classList.toggle('is-selected', isSelected);
            });
            const activeInput = optionInputs[selectedType];
            if (activeInput && !activeInput.readOnly) {
                setTimeout(() => activeInput.focus(), 0);
            }
        }

        optionDefs.forEach(def => {
            const row = document.createElement('div');
            row.className = 'link-option-row' + (def.type === initialType ? ' is-selected' : '') + (def.hasBrowse ? ' link-option-row--browse' : '');
            optionRows[def.type] = row;

            // Radio + label
            const radioLabel = document.createElement('label');
            radioLabel.className = 'link-option-label';
            const radio = document.createElement('input');
            radio.type = 'radio';
            radio.name = 'linkPickerType';
            radio.value = def.type;
            radio.checked = def.type === initialType;
            radioLabel.appendChild(radio);
            const labelSpan = document.createElement('span');
            labelSpan.textContent = def.label;
            radioLabel.appendChild(labelSpan);
            row.appendChild(radioLabel);
            optionRadios[def.type] = radio;

            // Input area
            const inputWrap = document.createElement('div');
            inputWrap.className = 'link-option-input-wrap' + (def.hasBrowse ? ' link-option-input-wrap--browse' : '');
            let valueName = null;

            if (def.hasBrowse) {
                if (def.initValue) inputWrap.classList.add('has-value');
                // Value display (doc icon + page name) — visible in has-value state
                const valueDisplay = document.createElement('span');
                valueDisplay.className = 'link-option-value-display';
                const valueDocIcon = document.createElement('span');
                valueDocIcon.className = 'link-option-value-icon';
                valueDocIcon.innerHTML = '<i class="far fa-file-alt"></i>';
                valueName = document.createElement('span');
                valueName.className = 'link-option-value-name';
                valueName.textContent = def.initValue || '';
                valueDisplay.appendChild(valueDocIcon);
                valueDisplay.appendChild(valueName);
                inputWrap.appendChild(valueDisplay);
            }

            const input = document.createElement('input');
            input.type = def.inputType;
            input.className = 'link-picker-input';
            input.placeholder = def.placeholder;
            input.value = def.initValue;
            input.disabled = def.type !== initialType;
            if (def.readonly) input.readOnly = true;
            inputWrap.appendChild(input);
            optionInputs[def.type] = input;

            row.appendChild(inputWrap);

            if (def.hasBrowse) {
                const browseBtn = document.createElement('button');
                browseBtn.type = 'button';
                browseBtn.className = 'link-picker-browse';
                browseBtn.textContent = def.browseLabel || 'Browse';
                browseBtn.disabled = def.type !== initialType;
                browseBtn.addEventListener('click', e => {
                    e.stopPropagation();
                    overlay.remove();
                    openPagePickerModal((_id, _wid, _pn, _name, url) => {
                        input.value = url;
                        if (valueName) valueName.textContent = _name || url;
                        inputWrap.classList.toggle('has-value', !!url);
                        currentValue = url;
                        renderDisplay(currentValue);
                        markFormAsDirty();
                    }, def.browseTypeFilter ?? 'all', def.browseLabel);
                });
                inputWrap.appendChild(browseBtn);
                optionBrowse[def.type] = browseBtn;
            }

            // Clicking anywhere on the row selects this option
            row.addEventListener('click', e => {
                if (e.target !== radio) {
                    radio.checked = true;
                }
                syncSelected(def.type);
            });

            radio.addEventListener('change', () => syncSelected(def.type));

            body.appendChild(row);
        });

        modal.appendChild(body);

        // Footer
        const footer = document.createElement('div');
        footer.className = 'link-picker-footer';

        const cancelBtn = document.createElement('button');
        cancelBtn.type = 'button';
        cancelBtn.className = 'btn btn-secondary';
        cancelBtn.textContent = 'Cancel';
        cancelBtn.addEventListener('click', () => overlay.remove());

        const okBtn = document.createElement('button');
        okBtn.type = 'button';
        okBtn.className = 'btn btn-primary';
        okBtn.textContent = 'OK';
        okBtn.addEventListener('click', () => {
            const selectedType = optionDefs.find(d => optionRadios[d.type].checked)?.type ?? 'external';
            let val = optionInputs[selectedType].value.trim();
            if (selectedType === 'email' && val && !/^mailto:/i.test(val)) val = 'mailto:' + val;
            currentValue = val;
            renderDisplay(currentValue);
            markFormAsDirty();
            overlay.remove();
        });

        footer.appendChild(cancelBtn);
        footer.appendChild(okBtn);
        modal.appendChild(footer);

        overlay.appendChild(modal);
        document.body.appendChild(overlay);
        overlay.addEventListener('click', e => { if (e.target === overlay) overlay.remove(); });

        // Focus the active input
        const firstActive = optionInputs[initialType];
        if (firstActive && !firstActive.readOnly) firstActive.focus();
    }

    addBtn.addEventListener('click', () => openLinkModal(currentValue || null));

    // Render initial state
    renderDisplay(currentValue);

    Object.defineProperty(container, 'value', {
        get() { return currentValue || ''; }
    });

    return container;
}

// ──────────────────────────────────────────────────────────────────────────
// Page Reference widget (ContentReference)
// ──────────────────────────────────────────────────────────────────────────

function createPageReferenceInput(name, fieldSchema, initialValue) {
    const wrapper = document.createElement('div');
    wrapper.className = 'page-reference-field';

    // Hidden input stores JSON: {"id":5,"workId":0,"providerName":null,"name":"...","url":"..."}
    const hidden = document.createElement('input');
    hidden.type = 'hidden';
    hidden.id = `input-${name}`;
    hidden.name = name;
    hidden.value = (initialValue && initialValue.id) ? JSON.stringify(initialValue) : '';
    wrapper.appendChild(hidden);

    // ── Display area (always visible, shows placeholder when empty) ──
    const display = document.createElement('div');
    display.className = 'epi-picker-display';
    wrapper.appendChild(display);

    // ── Action row: button always visible, text changes with state ──
    const actionRow = document.createElement('div');
    actionRow.className = 'epi-picker-actions';
    const selectBtn = document.createElement('button');
    selectBtn.type = 'button';
    selectBtn.className = 'epi-picker-btn';
    selectBtn.textContent = 'Select Content';
    actionRow.appendChild(selectBtn);
    wrapper.appendChild(actionRow);

    function setDisplay(pageName, pageUrl, contentType) {
        display.innerHTML = '';
        if (pageName) {
            const iconClass = _getContentIcon(contentType);

            const iconSpan = document.createElement('span');
            iconSpan.className = 'epi-picker-display__icon';
            const iconEl = document.createElement('i');
            iconEl.className = iconClass;
            iconSpan.appendChild(iconEl);
            display.appendChild(iconSpan);

            const textSpan = document.createElement('span');
            textSpan.className = 'epi-picker-display__text';
            textSpan.textContent = pageName;
            display.appendChild(textSpan);

            if (pageUrl) {
                const urlSpan = document.createElement('span');
                urlSpan.className = 'epi-picker-display__url';
                urlSpan.textContent = pageUrl;
                display.appendChild(urlSpan);
            }

            const removeBtn = document.createElement('button');
            removeBtn.type = 'button';
            removeBtn.className = 'epi-picker-display__remove';
            removeBtn.title = 'Remove';
            const timesIcon = document.createElement('i');
            timesIcon.className = 'fas fa-times';
            removeBtn.appendChild(timesIcon);
            removeBtn.addEventListener('click', () => {
                setRef(null, 0, null, null, null, null);
            });
            display.appendChild(removeBtn);
        } else {
            const placeholder = document.createElement('span');
            placeholder.className = 'epi-picker-display__placeholder';
            placeholder.textContent = 'No content selected';
            display.appendChild(placeholder);
        }
    }

    function setRef(id, workId, providerName, pageName, pageUrl, contentType) {
        if (id) {
            // Store name+url+contentType alongside the reference so we can restore display without a re-fetch
            hidden.value = JSON.stringify({ id, workId: workId ?? 0, providerName: providerName ?? null, name: pageName ?? null, url: pageUrl ?? null, contentType: contentType ?? null });
            setDisplay(pageName, pageUrl, contentType);
        } else {
            hidden.value = '';
            setDisplay(null, null, null);
        }
        markFormAsDirty();
    }

    // If initial value has an id, use stored name if available; otherwise fetch from our own API
    if (initialValue && initialValue.id) {
        if (initialValue.name) {
            // Name was persisted in the JSON — use it directly
            setDisplay(initialValue.name, initialValue.url ?? null, initialValue.contentType ?? null);
        } else {
            // Fetch name from our content-by-id endpoint
            const lang = document.getElementById('languageSelector')?.value || '';
            fetch(`/customsettings/api/content/${initialValue.id}?language=${lang}`, { credentials: 'same-origin' })
                .then(r => r.ok ? r.json() : null)
                .then(data => {
                    const pageName    = data?.name        ?? `Content #${initialValue.id}`;
                    const pageUrl     = data?.url         ?? null;
                    const contentType = data?.contentType ?? null;
                    setDisplay(pageName, pageUrl, contentType);
                    // Update hidden value with name so future loads don't need to re-fetch
                    hidden.value = JSON.stringify({ id: initialValue.id, workId: initialValue.workId ?? 0, providerName: initialValue.providerName ?? null, name: pageName, url: pageUrl, contentType });
                })
                .catch(() => setDisplay(`Content #${initialValue.id}`, null, null));
        }
    }

    // Select Content button → open modal
    selectBtn.addEventListener('click', () => openPagePickerModal((id, workId, providerName, pageName, pageUrl, contentType) => {
        setRef(id, workId, providerName, pageName, pageUrl, contentType);
    }));

    return wrapper;
}

function openPagePickerModal(onSelect, typeFilter, title) {
    // Remove existing modal if any
    document.getElementById('pagePickerModal')?.remove();

    const overlay = document.createElement('div');
    overlay.id = 'pagePickerModal';
    overlay.className = 'page-picker-overlay cs-picker-root';

    const modal = document.createElement('div');
    modal.className = 'page-picker-modal';

    // Header
    const header = document.createElement('div');
    header.className = 'page-picker-header';
    header.innerHTML = '<span>' + (title || 'Select Content') + '</span>';
    const closeBtn = document.createElement('button');
    closeBtn.type = 'button';
    closeBtn.className = 'page-picker-close';
    closeBtn.innerHTML = '&times;';
    closeBtn.addEventListener('click', () => overlay.remove());
    header.appendChild(closeBtn);
    modal.appendChild(header);

    // Search input wrapped in grey area container
    const searchArea = document.createElement('div');
    searchArea.className = 'page-picker-search-area';
    const searchInput = document.createElement('input');
    searchInput.type = 'text';
    searchInput.className = 'page-picker-search';
    searchInput.placeholder = 'Search content by name...';
    searchArea.appendChild(searchInput);
    modal.appendChild(searchArea);

    // Results list
    const resultsList = document.createElement('ul');
    resultsList.className = 'page-picker-results';
    resultsList.innerHTML = '<li class="page-picker-result page-picker-result--loading"><i class="fas fa-spinner fa-spin"></i> Loading content...</li>';
    modal.appendChild(resultsList);

    overlay.appendChild(modal);
    document.body.appendChild(overlay);
    searchInput.focus();

    // Close on overlay click
    overlay.addEventListener('click', (e) => { if (e.target === overlay) overlay.remove(); });

    async function searchPages(q) {
        const lang = document.getElementById('languageSelector')?.value || '';
        const tf   = typeFilter ? `&type=${encodeURIComponent(typeFilter)}` : '';
        const qs   = `?language=${lang}${tf}${q ? '&q=' + encodeURIComponent(q) : ''}`;
        try {
            const res = await fetch(`/customsettings/api/content/search${qs}`, { credentials: 'same-origin' });
            return (await res.json()).pages || [];
        } catch { return []; }
    }


    function createTreeItem(node, depth, onSelect, overlay) {
        const hasChildren = node.children && node.children.length > 0;
        const li = document.createElement('li');
        li.className = 'page-picker-result page-picker-tree-item';
        li.dataset.depth = depth;

        // Row
        const row = document.createElement('div');
        row.className = 'page-picker-row';
        row.style.paddingLeft = `${14 + depth * 20}px`;

        // Toggle (expand/collapse) for parents
        const toggle = document.createElement('span');
        toggle.className = 'page-picker-toggle';
        if (hasChildren) {
            toggle.innerHTML = '<i class="fas fa-chevron-down page-picker-chevron"></i>';
        } else {
            toggle.innerHTML = '<span class="page-picker-toggle-spacer"></span>';
        }
        row.appendChild(toggle);

        // Icon based on content type
        const icon = document.createElement('span');
        icon.className = 'page-picker-icon page-picker-icon--page';
        const iconEl = document.createElement('i');
        iconEl.className = _getContentIcon(node.contentType);
        icon.appendChild(iconEl);
        row.appendChild(icon);

        // Name + URL
        const info = document.createElement('span');
        info.className = 'page-picker-info';
        const nameSpan = document.createElement('span');
        nameSpan.className = 'page-picker-result-name';
        nameSpan.textContent = node.name;
        info.appendChild(nameSpan);
        const urlSpan = document.createElement('span');
        urlSpan.className = 'page-picker-result-url';
        urlSpan.textContent = node.url;
        info.appendChild(urlSpan);
        row.appendChild(info);

        li.appendChild(row);

        // Children container
        let childrenUl = null;
        let expanded = true;
        if (hasChildren) {
            childrenUl = document.createElement('ul');
            childrenUl.className = 'page-picker-children';
            node.children.forEach(child => {
                childrenUl.appendChild(createTreeItem(child, depth + 1, onSelect, overlay));
            });
            li.appendChild(childrenUl);

            // Toggle collapse/expand
            toggle.style.cursor = 'pointer';
            toggle.addEventListener('click', (e) => {
                e.stopPropagation();
                expanded = !expanded;
                childrenUl.style.display = expanded ? '' : 'none';
                const chevron = toggle.querySelector('.page-picker-chevron');
                if (chevron) chevron.style.transform = expanded ? '' : 'rotate(-90deg)';
            });
        }

        // Click on row → select if selectable, otherwise just toggle expand
        row.addEventListener('click', (e) => {
            if (e.target.closest('.page-picker-toggle')) return;
            if (node.selectable === false) {
                // Folder: toggle children
                if (hasChildren) {
                    expanded = !expanded;
                    if (childrenUl) childrenUl.style.display = expanded ? '' : 'none';
                    const chevron = toggle.querySelector('.page-picker-chevron');
                    if (chevron) chevron.style.transform = expanded ? '' : 'rotate(-90deg)';
                }
                return;
            }
            onSelect(node.id, node.workId ?? 0, node.providerName ?? null, node.name, node.url, node.contentType ?? null);
            overlay.remove();
        });

        // Visual cue for non-selectable folders
        if (node.selectable === false) {
            row.style.cursor = 'default';
            info.style.color = '#555';
        }

        return li;
    }

    function renderTree(pages) {
        resultsList.innerHTML = '';
        if (!pages.length) {
            resultsList.innerHTML = '<li class="page-picker-result page-picker-result--empty">No content found</li>';
            return;
        }
        const roots = _buildPageTree(pages);
        roots.forEach(root => {
            resultsList.appendChild(createTreeItem(root, 0, onSelect, overlay));
        });
    }

    function renderFlat(pages) {
        resultsList.innerHTML = '';
        if (!pages.length) {
            resultsList.innerHTML = '<li class="page-picker-result page-picker-result--empty">No content found</li>';
            return;
        }
        pages.forEach(({ name, url, id, workId, providerName, contentType, selectable }) => {
            if (selectable === false) return; // skip folders in flat search results
            const li = document.createElement('li');
            li.className = 'page-picker-result';
            const parts = url ? url.replace(/^\/|\/$/g, '').split('/').filter(Boolean) : [];
            const breadcrumb = parts.length > 1 ? parts.slice(0, -1).join(' \u203a ') : null;

            const row = document.createElement('div');
            row.className = 'page-picker-row';
            row.style.paddingLeft = '14px';

            const iconSpan = document.createElement('span');
            iconSpan.className = 'page-picker-icon page-picker-icon--page';
            const iconEl = document.createElement('i');
            iconEl.className = _getContentIcon(contentType);
            iconSpan.appendChild(iconEl);
            row.appendChild(iconSpan);

            const infoSpan = document.createElement('span');
            infoSpan.className = 'page-picker-info';

            const nameSpan = document.createElement('span');
            nameSpan.className = 'page-picker-result-name';
            nameSpan.textContent = name;
            infoSpan.appendChild(nameSpan);

            if (breadcrumb) {
                const bcSpan = document.createElement('span');
                bcSpan.className = 'page-picker-breadcrumb';
                bcSpan.textContent = breadcrumb;
                infoSpan.appendChild(bcSpan);
            }

            if (url) {
                const urlSpan = document.createElement('span');
                urlSpan.className = 'page-picker-result-url';
                urlSpan.textContent = url;
                infoSpan.appendChild(urlSpan);
            }

            row.appendChild(infoSpan);
            li.appendChild(row);

            li.addEventListener('click', () => {
                onSelect(id, workId ?? 0, providerName ?? null, name, url, contentType ?? null);
                overlay.remove();
            });
            resultsList.appendChild(li);
        });
    }

    // Initial load — tree view
    searchPages('').then(renderTree);

    // Live search — flat view
    let debounce;
    searchInput.addEventListener('input', () => {
        clearTimeout(debounce);
        resultsList.innerHTML = '<li class="page-picker-result page-picker-result--loading"><i class="fas fa-spinner fa-spin"></i> Searching...</li>';
        if (!searchInput.value.trim()) {
            debounce = setTimeout(() => searchPages('').then(renderTree), 250);
        } else {
            debounce = setTimeout(() => searchPages(searchInput.value).then(renderFlat), 250);
        }
    });
}
