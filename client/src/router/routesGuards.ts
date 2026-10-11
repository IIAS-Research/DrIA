import { useUserStore } from 'src/stores/user-store';
import { notify } from 'src/utils/notify';
import { NavigationGuardNext, RouteLocationNormalized } from 'vue-router';

export function mustBeAuth(
  to: RouteLocationNormalized,
  from: RouteLocationNormalized,
  next: NavigationGuardNext,
): void {
  const store = useUserStore();
  if (!store.checkIfUserIsConnected()) {
    next({ name: 'login', query: { redirect: to.fullPath } });
  } else next();
}

export function mustNotBeAuth(
  to: RouteLocationNormalized,
  from: RouteLocationNormalized,
  next: NavigationGuardNext,
): void {
  const store = useUserStore();
  if (store.checkIfUserIsConnected()) {
    next({ name: 'home' });
  } else next();
}

export function mustBeAdmin(
  to: RouteLocationNormalized,
  from: RouteLocationNormalized,
  next: NavigationGuardNext,
): void {
  const store = useUserStore();
  if (!store.currentUser?.isAdmin) {
    notify('negative', 'Vous devez être administrateur pour accéder à cette ressource 😡');
    next(from);
  } else next();
}
