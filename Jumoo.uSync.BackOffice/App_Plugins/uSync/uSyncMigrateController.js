(function () {
    angular.module('umbraco')
        .controller('uSyncMigrateController', uSyncMigrateController);

    uSyncMigrateController.$inject = ['$scope', 'uSyncDashboardService'];
    function uSyncMigrateController($scope, uSyncDashboardService) {
        $scope.loading = true;
        uSyncDashboardService.getSettings()
            .then(function (response) {
                $scope.settings = response.data.settings;
                $scope.loading = false;
            });
    }
})();