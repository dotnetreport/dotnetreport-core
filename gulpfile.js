var gulp = require('gulp');
var uglify = require('gulp-uglify');
var concat = require('gulp-concat');
var rimraf = require("rimraf");
var merge = require('merge-stream');

gulp.task("minify", function () {

	var streams = [
		gulp.src(["wwwroot/js/*.js"])
			.pipe(uglify())
			.pipe(concat("site.min.js"))
			.pipe(gulp.dest("wwwroot/lib/site"))
	];

	return merge(streams);
});

// Dependency Dirs
var deps = {
	"bootstrap": {
		"dist/**/*": ""
	},
	"jquery": {
		"dist/*": ""
	},
	"jquery-validation": {
		"dist/**/*": ""
	},
	"jquery-validation-unobtrusive": {
		"dist/*": ""
	},
	"knockout": {
		"build/output/*": ""
	},
	"lodash": {
		"lodash.min.js": ""
	},
	"toastr": {
		"build/*": ""
	},
	"bootstrap-datepicker": {
		"dist/**/*": ""
	},
	"font-awesome": {
		"**/*": ""
	},
	"select2": {
		"dist/**/*": ""
	},
	"jquery-blockui": {
		"jquery.blockUI.js": ""
	},
	"bootbox": {
		"dist/*": ""
	},
	"knockout-sortable": {
		"build/*": ""
	}
};

gulp.task("clean", function (cb) {
	return rimraf("wwwroot/lib/", cb);
});

gulp.task("scripts", function () {

	var streams = [];

	for (var prop in deps) {
		console.log("Prepping Scripts for: " + prop);
		for (var itemProp in deps[prop]) {
			streams.push(gulp.src("node_modules/" + prop + "/" + itemProp)
				.pipe(gulp.dest("wwwroot/lib/" + prop + "/" + deps[prop][itemProp])));
		}
	}

	return merge(streams);

});

gulp.task('build', gulp.series(
	'clean',
	'minify',
	'scripts'));
