var serverConfigForm = document.getElementById("serverConfigForm") as HTMLFormElement;
var serverConfigSaveButton = document.getElementById("serverConfigSaveButton") as HTMLButtonElement;

var trustedCorsAddButton = document.getElementById("trustedCorsAddButton") as HTMLButtonElement;
var trustedCorsRemoveButton = document.getElementById("trustedCorsRemoveButton") as HTMLButtonElement;
var trustedCorsInput = document.getElementById("trustedCorsInput") as HTMLInputElement;
var trustedCorsSelect = document.getElementById("trustedCorsSelect") as HTMLSelectElement;

var knownProxiesAddButton = document.getElementById("knownProxiesAddButton") as HTMLButtonElement;
var knownProxiesRemoveButton = document.getElementById("knownProxiesRemoveButton") as HTMLButtonElement;
var knownProxiesInput = document.getElementById("knownProxiesInput") as HTMLInputElement;
var knownProxiesSelect = document.getElementById("knownProxiesSelect") as HTMLSelectElement;

var serverAdminsAddButton = document.getElementById("serverAdminsAddButton") as HTMLButtonElement;
var serverAdminsRemoveButton = document.getElementById("serverAdminsRemoveButton") as HTMLButtonElement;
var serverAdminsInput = document.getElementById("serverAdminsInput") as HTMLInputElement;
var serverAdminsSelect = document.getElementById("serverAdminsSelect") as HTMLSelectElement;

serverConfigSaveButton.addEventListener("click", e => {
    for (var i = 0; i < trustedCorsSelect.options.length; i++) {
        trustedCorsSelect.options[i].selected = true;
    }
    for (var i = 0; i < knownProxiesSelect.options.length; i++) {
        knownProxiesSelect.options[i].selected = true;
    }
    for (var i = 0; i < serverAdminsSelect.options.length; i++) {
        serverAdminsSelect.options[i].selected = true;
    }

    serverConfigForm.submit();
});

trustedCorsAddButton.addEventListener("click", ev => {
    if (trustedCorsInput.value.length > 0) {
        var option = document.createElement("option");
        option.value = trustedCorsInput.value;
        option.text = trustedCorsInput.value;
        trustedCorsSelect.add(option);
        trustedCorsInput.value = "";
    }
});

trustedCorsInput.addEventListener("keypress", ev => {
    if (ev.key.toLowerCase() == "enter") {
        ev.preventDefault();
        ev.stopPropagation();
        trustedCorsAddButton.click();
    }
})

trustedCorsRemoveButton.addEventListener("click", ev => {
    while (trustedCorsSelect.selectedOptions.length > 0) {
        trustedCorsSelect.selectedOptions[0].remove();
    }
});


knownProxiesAddButton.addEventListener("click", ev => {
    if (knownProxiesInput.value.length > 0) {
        var option = document.createElement("option");
        option.value = knownProxiesInput.value;
        option.text = knownProxiesInput.value;
        knownProxiesSelect.add(option);
        knownProxiesInput.value = "";
    }
});

knownProxiesInput.addEventListener("keypress", ev => {
    if (ev.key.toLowerCase() == "enter") {
        ev.preventDefault();
        ev.stopPropagation();
        knownProxiesAddButton.click();
    }
})

knownProxiesRemoveButton.addEventListener("click", ev => {
    while (knownProxiesSelect.selectedOptions.length > 0) {
        knownProxiesSelect.selectedOptions[0].remove();
    }
});


serverAdminsAddButton.addEventListener("click", ev => {
    if (serverAdminsInput.value.length > 0) {
        var option = document.createElement("option");
        option.value = serverAdminsInput.value;
        option.text = serverAdminsInput.value;
        serverAdminsSelect.add(option);
        serverAdminsInput.value = "";
    }
});

serverAdminsInput.addEventListener("keypress", ev => {
    if (ev.key.toLowerCase() == "enter") {
        ev.preventDefault();
        ev.stopPropagation();
        serverAdminsAddButton.click();
    }
})

serverAdminsRemoveButton.addEventListener("click", ev => {
    while (serverAdminsSelect.selectedOptions.length > 0) {
        serverAdminsSelect.selectedOptions[0].remove();
    }
});
