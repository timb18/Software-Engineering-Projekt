SET statement_timeout = 0;
SET lock_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET client_min_messages = warning;
SET row_security = off;

CREATE TYPE public.invitation_status AS ENUM (
    'open',
    'closed',
    'accepted',
    'expired'
);

CREATE TYPE public.role AS ENUM (
    'user',
    'organizer'
);

CREATE TYPE public.task_intensity AS ENUM (
    'light',
    'normal',
    'intensive'
);

CREATE TYPE public.task_priority AS ENUM (
    'low',
    'medium',
    'high'
);

SET default_tablespace = '';
SET default_table_access_method = heap;

CREATE TABLE public.users (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    username character varying(255),
    email character varying(40) NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    edited_at timestamp with time zone,
    CONSTRAINT valid_email CHECK (((email)::text ~* '^[A-Za-z0-9._+%-]+@[A-Za-z0-9.-]+[.][A-Za-z]+$'::text))
);

CREATE TABLE public.organizations (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(100) NOT NULL,
    max_users integer NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    edited_at timestamp with time zone
);

CREATE TABLE public.memberships (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    organization_id uuid NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    edited_at timestamp with time zone,
    role public.role NOT NULL
);

CREATE TABLE public.invitations (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    organization_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    edited_at timestamp with time zone,
    status public.invitation_status NOT NULL,
    expiry_date timestamp with time zone
);

CREATE TABLE public.work_profiles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    membership_id uuid NOT NULL,
    max_daily_load interval NOT NULL,
    planner_view_start character varying(5) NOT NULL DEFAULT '06:00',
    planner_view_end character varying(5) NOT NULL DEFAULT '22:00',
    created_at timestamp with time zone NOT NULL,
    edited_at timestamp with time zone
);

CREATE TABLE public.work_profile_time_intervals (
    work_profile_id uuid CONSTRAINT time_intervals_work_profile_id_not_null NOT NULL,
    start_date timestamp with time zone CONSTRAINT time_intervals_start_date_not_null NOT NULL,
    end_date timestamp with time zone CONSTRAINT time_intervals_end_date_not_null NOT NULL
);

CREATE TABLE public.work_day_profiles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    work_profile_id uuid NOT NULL,
    day character varying(3) NOT NULL
);

CREATE TABLE public.work_blocks (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    work_day_profile_id uuid NOT NULL,
    company_id character varying NOT NULL DEFAULT '',
    company_name character varying NOT NULL DEFAULT '',
    start_time character varying(5) NOT NULL DEFAULT '09:00',
    end_time character varying(5) NOT NULL DEFAULT '17:00'
);

CREATE TABLE public.work_breaks (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    work_day_profile_id uuid NOT NULL,
    start_time character varying(5) NOT NULL DEFAULT '12:00',
    end_time character varying(5) NOT NULL DEFAULT '12:30'
);

CREATE TABLE public.user_tasks (
    id uuid DEFAULT gen_random_uuid() CONSTRAINT tasks_id_not_null NOT NULL,
    work_profile_id uuid CONSTRAINT tasks_work_profile_id_not_null NOT NULL,
    description character varying(255),
    priority public.task_priority CONSTRAINT tasks_priority_not_null NOT NULL,
    is_fixed boolean CONSTRAINT tasks_is_fixed_not_null NOT NULL,
    time_estimate interval CONSTRAINT tasks_time_estimate_not_null NOT NULL,
    deadline timestamp with time zone,
    created_at timestamp with time zone DEFAULT now() CONSTRAINT tasks_created_at_not_null NOT NULL,
    edited_at timestamp with time zone,
    intensity public.task_intensity NOT NULL,
    name character varying(30) NOT NULL,
    early_start timestamp with time zone NOT NULL,
    early_finish timestamp with time zone NOT NULL,
    late_start timestamp with time zone NOT NULL,
    late_finish timestamp with time zone NOT NULL,
    status character varying(20) NOT NULL DEFAULT 'todo'
);

CREATE TABLE public.task_blocks (
    task_id uuid CONSTRAINT task_timeblocks_task_id_not_null NOT NULL,
    start_date timestamp with time zone CONSTRAINT task_timeblocks_start_date_not_null NOT NULL,
    end_date timestamp with time zone CONSTRAINT task_timeblocks_end_date_not_null NOT NULL,
    is_fixed boolean CONSTRAINT task_timeblocks_is_fixed_not_null NOT NULL
);

CREATE TABLE public.task_dependencies (
    task_id uuid NOT NULL,
    depends_on_task_id uuid NOT NULL
);

-- Primary Keys
ALTER TABLE ONLY public.users ADD CONSTRAINT users_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.users ADD CONSTRAINT users_email_key UNIQUE (email);
ALTER TABLE ONLY public.organizations ADD CONSTRAINT organizations_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.memberships ADD CONSTRAINT memberships_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.invitations ADD CONSTRAINT invitations_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.work_profiles ADD CONSTRAINT work_profiles_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.work_profiles ADD CONSTRAINT work_profiles_membership_id_key UNIQUE (membership_id);
ALTER TABLE ONLY public.work_day_profiles ADD CONSTRAINT work_day_profiles_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.work_blocks ADD CONSTRAINT work_blocks_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.work_breaks ADD CONSTRAINT work_breaks_pkey PRIMARY KEY (id);
ALTER TABLE ONLY public.user_tasks ADD CONSTRAINT tasks_pkey PRIMARY KEY (id);

-- Foreign Keys
ALTER TABLE ONLY public.memberships ADD CONSTRAINT memberships_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id);
ALTER TABLE ONLY public.memberships ADD CONSTRAINT memberships_organization_id_fkey FOREIGN KEY (organization_id) REFERENCES public.organizations(id);
ALTER TABLE ONLY public.invitations ADD CONSTRAINT invitations_organization_id_fkey FOREIGN KEY (organization_id) REFERENCES public.organizations(id);
ALTER TABLE ONLY public.invitations ADD CONSTRAINT invitations_created_by_fkey FOREIGN KEY (created_by) REFERENCES public.users(id);
ALTER TABLE ONLY public.work_profiles ADD CONSTRAINT work_profiles_membership_id_fkey FOREIGN KEY (membership_id) REFERENCES public.memberships(id) NOT VALID;
ALTER TABLE ONLY public.work_profile_time_intervals ADD CONSTRAINT time_intervals_work_profile_id_fkey FOREIGN KEY (work_profile_id) REFERENCES public.work_profiles(id);
ALTER TABLE ONLY public.work_day_profiles ADD CONSTRAINT work_day_profiles_work_profile_id_fkey FOREIGN KEY (work_profile_id) REFERENCES public.work_profiles(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.work_blocks ADD CONSTRAINT work_blocks_work_day_profile_id_fkey FOREIGN KEY (work_day_profile_id) REFERENCES public.work_day_profiles(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.work_breaks ADD CONSTRAINT work_breaks_work_day_profile_id_fkey FOREIGN KEY (work_day_profile_id) REFERENCES public.work_day_profiles(id) ON DELETE CASCADE;
ALTER TABLE ONLY public.user_tasks ADD CONSTRAINT tasks_workprofile_id_fkey FOREIGN KEY (work_profile_id) REFERENCES public.work_profiles(id) NOT VALID;
ALTER TABLE ONLY public.task_blocks ADD CONSTRAINT task_timeslots_task_id_fkey FOREIGN KEY (task_id) REFERENCES public.user_tasks(id);
ALTER TABLE ONLY public.task_dependencies ADD CONSTRAINT task_dependencies_task_id_fkey FOREIGN KEY (task_id) REFERENCES public.user_tasks(id);
ALTER TABLE ONLY public.task_dependencies ADD CONSTRAINT task_dependencies_depends_on_task_id_fkey FOREIGN KEY (depends_on_task_id) REFERENCES public.user_tasks(id);

REVOKE USAGE ON SCHEMA public FROM PUBLIC;
GRANT ALL ON SCHEMA public TO PUBLIC;
