$(document).ready(function () {
    var indicator = document.querySelector(".indicator");
    var input = document.getElementById("PasswordStrength");
    var weak = document.querySelector(".weak");
    var medium = document.querySelector(".medium");
    var strong = document.querySelector(".strong");
    var text = document.querySelector(".text");
    var regExpWeak = /[a-z]/;
    var regExpMedium = /\d+/;
    var regExpStrong = /.[!,@,#,$,%,^,&,*,?,_,~,-,(,)]/;


    function trigger() {
        if (input.value != "") {
            indicator.style.display = "block";
            indicator.style.display = "flex";
            if (input.value.length <= 3 && (input.value.match(regExpWeak) || input.value.match(regExpMedium) || input.value.match(regExpStrong))) no = 1;
            if (input.value.length >= 6 && ((input.value.match(regExpWeak) && input.value.match(regExpMedium)) || (input.value.match(regExpMedium) && input.value.match(regExpStrong)) || (input.value.match(regExpWeak) && input.value.match(regExpStrong)))) no = 2;
            if (input.value.length >= 6 && input.value.match(regExpWeak) && input.value.match(regExpMedium) && input.value.match(regExpStrong)) no = 3;
            if (no == 1) {
                weak.classList.add("active");
                text.style.display = "block";
                text.textContent = "Your password is too week";
                text.classList.add("weak");
            }
            if (no == 2) {
                medium.classList.add("active");
                text.textContent = "Your password is medium";
                text.classList.add("medium");
            } else {
                medium.classList.remove("active");
                text.classList.remove("medium");
            }
            if (no == 3) {
                weak.classList.add("active");
                medium.classList.add("active");
                strong.classList.add("active");
                text.textContent = "Your password is strong";
                text.classList.add("strong");
            } else {
                strong.classList.remove("active");
                text.classList.remove("strong");
            }
        } else {
            indicator.style.display = "none";
            text.style.display = "none";
        }
    }

    // Whenever the key is pressed, apply condition checks.  
    $("#PasswordStrength").keyup(function () {
        trigger();
    });
});