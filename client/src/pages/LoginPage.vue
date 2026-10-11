<script setup lang="ts">
import { useUserStore } from 'stores/user-store';
import { reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

const userStore = useUserStore();
const router = useRouter();
const route = useRoute();
const submitting = ref(false);
const showPassword = ref(false);

const login = reactive({
  email: '',
  password: '',
});

async function submit() {
  if (submitting.value) return;
  submitting.value = true;
  try {
    if (!await userStore.login(login)) return;
    const redirect = route.query.redirect;
    if (typeof redirect === 'string' && redirect.startsWith('/') && !redirect.startsWith('//')) {
      const destination = router.resolve(redirect);
      if (destination.matched.length && destination.name !== 'login') {
        await router.replace(redirect);
        return;
      }
    }
    await router.replace({ name: 'home' });
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <q-page class="login-page project-workspace-colors">
    <header class="login-header">
      <div class="login-brand" aria-label="DrIA">
        <img src="/images/baiddy_logo.svg" width="42" height="34" alt="">
        <span>DrIA</span>
      </div>
    </header>

    <main class="login-content">
      <section class="login-introduction" aria-labelledby="login-introduction-title">
        <p class="login-eyebrow">A guided approach to AI</p>
        <h1 id="login-introduction-title">From your data<br>to an <span>AI model.</span></h1>
        <p class="login-description">
          Explore your data, train models and understand their results
          in a workspace designed for medical research.
        </p>

        <ol class="login-workflow" aria-label="What you can do with DrIA">
          <li>
            <span class="login-step-number" aria-hidden="true">01</span>
            <div><strong>Explore your data</strong><span>Statistics and charts to understand your dataset.</span></div>
          </li>
          <li>
            <span class="login-step-number" aria-hidden="true">02</span>
            <div><strong>Train and compare</strong><span>A guided workflow to build and evaluate models.</span></div>
          </li>
          <li>
            <span class="login-step-number" aria-hidden="true">03</span>
            <div><strong>Understand predictions</strong><span>Explore results and the role of each variable.</span></div>
          </li>
        </ol>
      </section>

      <section class="login-form-panel" aria-labelledby="login-title">
        <div class="login-form-heading">
          <h2 id="login-title">Sign in to DrIA</h2>
          <p>Continue your projects or start a new study.</p>
        </div>

        <q-form class="login-form" :aria-busy="submitting" @submit="submit">
          <q-input
            v-model.trim="login.email"
            class="login-input"
            label="Email address"
            name="email"
            type="email"
            required
            autocomplete="username"
            autocapitalize="none"
            :spellcheck="false"
            outlined
            stack-label
            hide-bottom-space
            lazy-rules
            :disable="submitting"
            :rules="[
              (value) => !!value || 'Enter your email address.',
              (value, rules) => rules.email(value) || 'Enter a valid email address.',
            ]"
          />
          <q-input
            v-model="login.password"
            class="login-input"
            label="Password"
            name="password"
            :type="showPassword ? 'text' : 'password'"
            autocomplete="current-password"
            required
            outlined
            stack-label
            hide-bottom-space
            lazy-rules
            :disable="submitting"
            :rules="[(value) => !!value || 'Enter your password.']"
          >
            <template #append>
              <q-btn
                flat
                round
                dense
                :icon="showPassword ? 'visibility_off' : 'visibility'"
                :aria-label="showPassword ? 'Hide password' : 'Show password'"
                :aria-pressed="showPassword"
                :disable="submitting"
                @click="showPassword = !showPassword"
              />
            </template>
          </q-input>

          <q-btn
            class="login-submit"
            color="primary"
            type="submit"
            label="Sign in"
            no-caps
            unelevated
            :loading="submitting"
          />
        </q-form>

        <p class="login-account-help">Need an account? Contact your administrator.</p>
      </section>
    </main>

    <footer class="login-footer">
      <span>An IIAS project</span>
      <img src="/images/iias_miniature.png" alt="IIAS Research" height="32">
    </footer>
  </q-page>
</template>

<style scoped>
.login-page {
  display: flex;
  flex-direction: column;
  gap: 48px;
  padding: 32px clamp(24px, 5vw, 72px);
  color: var(--project-ink);
  background: var(--project-paper);
}
.login-header,
.login-content,
.login-footer { width: 100%; max-width: 1064px; margin-right: auto; margin-left: auto; }
.login-header { display: flex; align-items: center; justify-content: space-between; gap: 24px; }
.login-brand { display: flex; align-items: center; gap: 12px; }
.login-brand img { display: block; flex-shrink: 0; }
.login-brand span { font-size: 26px; font-weight: 700; letter-spacing: -.8px; }
.login-header-note { color: var(--project-muted); font-size: 12px; }
.login-content {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr);
  align-items: center;
  gap: clamp(48px, 7vw, 112px);
  flex: 1;
  padding: 24px 0;
}
.login-introduction { min-width: 0; }
.login-eyebrow { margin: 0 0 20px; color: var(--q-primary); font-size: 12px; font-weight: 600; letter-spacing: 1.2px; text-transform: uppercase; }
.login-introduction h1 { margin: 0; font-size: clamp(36px, 3.6vw, 48px); font-weight: 500; line-height: 1.18; letter-spacing: -1.7px; }
.login-introduction h1 span { color: var(--q-primary); }
.login-description { max-width: 410px; margin: 22px 0 0; color: var(--project-muted); font-size: 15px; line-height: 1.8; }
.login-workflow { display: grid; gap: 24px; margin: 36px 0 0; padding: 28px 0 0; border-top: 1px solid var(--project-border); list-style: none; }
.login-workflow li { display: flex; align-items: flex-start; gap: 16px; }
.login-step-number { flex-shrink: 0; min-width: 24px; padding-top: 2px; color: var(--q-primary); font-size: 12px; font-weight: 600; font-variant-numeric: tabular-nums; }
.login-workflow strong { display: block; margin-bottom: 4px; font-size: 14px; font-weight: 600; line-height: 1.4; }
.login-workflow li div > span { display: block; color: var(--project-muted); font-size: 12px; line-height: 1.6; }
.login-form-panel { min-width: 0; padding: 38px 36px 26px; border: 1px solid var(--project-border); border-radius: 16px; background: var(--project-surface); box-shadow: 0 12px 40px var(--project-shadow); }
.login-form-heading h2 { margin: 0; font-size: 26px; font-weight: 600; line-height: 1.3; letter-spacing: -.6px; }
.login-form-heading p { margin: 10px 0 0; color: var(--project-muted); font-size: 13px; line-height: 1.7; }
.login-form { display: grid; gap: 20px; margin-top: 30px; }
.login-input :deep(.q-field__control) { border-radius: 8px; }
.login-input :deep(.q-field__control::before) { border-color: var(--project-border); }
.login-input :deep(.q-field__label),
.login-input :deep(.q-field__append) { color: var(--project-muted); }
.login-submit { min-height: 48px; margin-top: 4px; border-radius: 8px; font-size: 14px; font-weight: 600; }
.login-submit:focus-visible,
.login-input :deep(.q-btn:focus-visible) { outline: 2px solid var(--q-primary); outline-offset: 3px; }
.login-account-help { margin: 28px 0 0; padding-top: 20px; border-top: 1px solid var(--project-border); color: var(--project-muted); font-size: 12px; line-height: 1.7; text-align: center; }
.login-footer { display: flex; align-items: center; justify-content: flex-end; gap: 14px; color: var(--project-muted); font-size: 11px; }
.login-footer img { display: block; width: auto; }

@media (min-width: 801px) and (max-height: 760px) {
  .login-page { gap: 28px; padding-top: 24px; padding-bottom: 24px; }
  .login-content { padding-top: 12px; padding-bottom: 12px; }
  .login-workflow { gap: 18px; margin-top: 24px; padding-top: 20px; }
}
@media (max-width: 800px) {
  .login-page { gap: 32px; padding: 24px; }
  .login-header,
  .login-content,
  .login-footer { max-width: 460px; }
  .login-content { grid-template-columns: minmax(0, 1fr); gap: 28px; padding: 0; }
  .login-eyebrow { margin-bottom: 12px; font-size: 11px; }
  .login-introduction h1 { font-size: 34px; letter-spacing: -1px; }
  .login-description { margin-top: 14px; font-size: 14px; line-height: 1.7; }
  .login-workflow { display: none; }
  .login-form-panel { padding: 28px; }
  .login-footer { justify-content: center; }
}
@media (max-width: 399px) {
  .login-page { padding: 20px 16px; }
  .login-header-note { font-size: 11px; }
  .login-form-panel { padding: 24px 20px; }
  .login-introduction h1 { font-size: 30px; }
}
</style>
